using System.Diagnostics;
using System.Globalization;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// OCR adapter that shells out to the system <c>tesseract</c> CLI (installed via apt
/// in the dev container and the prod image). We use the CLI rather than a P/Invoke
/// NuGet because the latter's native-library loading on Linux is brittle: it probes
/// version-pinned <c>lib*.dll.so</c> names that no distro ships and needs a <c>libdl</c>
/// shim on modern glibc. The CLI is Tesseract's first-class, stable interface.
/// </summary>
public class TesseractCliOcrService(IOptions<OcrOptions> options, ILogger<TesseractCliOcrService> logger) : IOcrService, IDisposable
{
    private readonly string _executable =
        string.IsNullOrWhiteSpace(options.Value.ExecutablePath) ? "tesseract" : options.Value.ExecutablePath!;
    private readonly string _tessdataPath = ResolveTessdataPath(options.Value);
    private readonly string _language = ResolveLanguage(options.Value.Language, logger);
    private readonly int _psm = options.Value.Psm;

    // Hard per-run ceiling so a hung/pathological process can't pin a worker forever.
    private readonly TimeSpan _timeout =
        TimeSpan.FromSeconds(options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 30);

    // Process-wide admission control: this adapter is a singleton, so this one
    // semaphore caps how many tesseract processes run at once across all requests.
    private readonly SemaphoreSlim _gate =
        new(options.Value.MaxConcurrency > 0 ? options.Value.MaxConcurrency : Environment.ProcessorCount * 2);

    // Cheap, side-effect-free check: the engine can only run if the language's
    // traineddata is present. A missing `tesseract` binary is handled in ReadAsync
    // (the process fails to start and we degrade to unavailable), so this stays a
    // pure file check, no process spawn just to test availability.
    public bool IsAvailable => File.Exists(Path.Combine(_tessdataPath, $"{_language}.traineddata"));

    // The concurrency gate is the only owned disposable. As a DI singleton this is
    // released when the container is disposed at shutdown.
    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default)
    {
        // Admission control: queue (don't spawn) once MaxConcurrency processes are live.
        // Cancelled while waiting ⇒ propagate as a normal caller cancellation (the gate
        // was never acquired, so there is nothing to release).
        await _gate.WaitAsync(ct);

        // Bound the child independently of the caller. The linked token fires when EITHER
        // the caller cancels OR our timeout elapses; the two cases are told apart in the
        // catch blocks below by inspecting the original ct.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(_timeout);
        var token = linked.Token;

        Process? process = null;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            var psi = new ProcessStartInfo(_executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            // Read the image from stdin (`-`), write recognised text to stdout, and pass
            // the tessdata dir explicitly: Tesseract 5's CLI treats TESSDATA_PREFIX as the
            // dir itself (older versions appended /tessdata), so --tessdata-dir is the
            // unambiguous, version-proof option.
            psi.ArgumentList.Add("-");
            psi.ArgumentList.Add("stdout");
            psi.ArgumentList.Add("-l");
            psi.ArgumentList.Add(_language);
            psi.ArgumentList.Add("--tessdata-dir");
            psi.ArgumentList.Add(_tessdataPath);
            // See OcrOptions.Psm: the default mode looks for the columns of a scanned
            // page, which is the wrong question to ask of a coffee bag.
            psi.ArgumentList.Add("--psm");
            psi.ArgumentList.Add(_psm.ToString(CultureInfo.InvariantCulture));
            // Ask for TSV rather than plain text: it carries per-word confidence and
            // bounding boxes, which is the only reliable way to tell printed label text
            // from background noise (a photo of a bag on a table produces a dozen
            // high-letter-count junk lines that look exactly like text otherwise).
            // The config name must come last.
            psi.ArgumentList.Add("tsv");

            process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Could not start '{_executable}'.");

            // Start draining stdout/stderr before writing stdin so a large image can't
            // deadlock against a full output pipe.
            stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            stderrTask = process.StandardError.ReadToEndAsync(token);

            await WriteUprightAsync(image, process.StandardInput.BaseStream, token);
            process.StandardInput.Close();

            await process.WaitForExitAsync(token);
            // Observe both pipes (stderr too, even on success) so neither read is left
            // as an unobserved task.
            var text = await stdoutTask;
            var error = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning("tesseract exited {ExitCode}: {Error}", process.ExitCode, error);
                return OcrResult.Unavailable;
            }

            return BuildResult(text);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller cancelled, so propagate rather than masquerading as an engine
            // outage (which would log noise and skew availability signals).
            throw;
        }
        catch (OperationCanceledException)
        {
            // Our own timeout fired (linked token, but the caller's ct is untouched):
            // treat as an engine outage → 503, not a client-cancelled request.
            logger.LogWarning(
                "tesseract exceeded the {TimeoutSeconds}s timeout; killing it and reporting unavailable.",
                _timeout.TotalSeconds);
            return OcrResult.Unavailable;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "tesseract CLI failed; reporting unavailable.");
            return OcrResult.Unavailable;
        }
        finally
        {
            if (process is not null)
            {
                // Dispose() does NOT terminate the child; on cancel/timeout/error the
                // process (possibly blocked waiting for stdin EOF) would otherwise leak.
                // Kill the tree, then observe the pipe reads (the kill unblocks them)
                // before dispose.
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Raced with exit, nothing to kill.
                }

                await Observe(stdoutTask);
                await Observe(stderrTask);
                process.Dispose();
            }

            _gate.Release();
        }
    }


    /// <summary>
    /// Pipes the image to the engine, turning it the right way up first when the camera
    /// said to.
    ///
    /// A phone does not rotate pixels when you turn it: it writes them in sensor order
    /// and records an EXIF orientation tag. Every viewer honours that tag, so the photo
    /// looks upright everywhere the user has seen it, and Leptonica, which is what
    /// Tesseract decodes JPEG with, does not. Without this, a bag photographed in
    /// portrait reaches the engine on its side and comes back as mirrored nonsense.
    /// </summary>
    private static async Task WriteUprightAsync(Stream image, Stream destination, CancellationToken ct)
    {
        // The header has to be read and then the same bytes replayed. The one caller
        // hands over a MemoryStream, but the port promises only a Stream.
        Stream source = image;
        MemoryStream? buffered = null;
        if (!image.CanSeek)
        {
            buffered = new MemoryStream();
            await image.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            source = buffered;
        }

        try
        {
            var start = source.Position;
            if (!await IsRotatedAsync(source, ct))
            {
                // The overwhelmingly common case, and it stays free: no decode, no
                // re-encode, the upload's own bytes straight down the pipe.
                source.Position = start;
                await source.CopyToAsync(destination, ct);
                return;
            }

            source.Position = start;
            using var picture = await Image.LoadAsync(source, ct);
            picture.Mutate(x => x.AutoOrient());
            // PNG, not JPEG: re-encoding would lay a second generation of block artefacts
            // over the camera's own, right on the glyph edges the engine reads.
            await picture.SaveAsync(destination, new PngEncoder(), ct);
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    /// <summary>Reads only the header, so an upright photo is never decoded twice.</summary>
    private static async Task<bool> IsRotatedAsync(Stream source, CancellationToken ct)
    {
        try
        {
            var info = await Image.IdentifyAsync(source, ct);
            return info.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var orientation) == true
                && orientation?.Value is > 1;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // Undecodable here is not this method's call to make: the application layer
            // already validated the bytes, so hand them over and let the engine refuse
            // them if it disagrees.
            return false;
        }
    }

    // Awaits a pipe-read to completion and discards any fault/cancellation, so a
    // read abandoned on the error path can't resurface as an unobserved-task exception.
    private static async Task Observe(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch
        {
            // Already handled by the caller's catch, or cancelled; ignore here.
        }
    }

    // Turns tesseract's TSV into lines carrying mean confidence and glyph height.
    //
    // Columns are: level page_num block_num par_num line_num word_num left top width
    // height conf text. level 5 is a word; coarser levels repeat the geometry with
    // conf = -1, so only words are read and then grouped by (block, par, line).
    //
    // Falls back to treating the output as plain text when it isn't TSV at all: a
    // stubbed binary in tests, or a future tesseract that changes the format. Callers
    // then simply get lines with no quality signals rather than an empty read.
    private static OcrResult BuildResult(string stdout)
    {
        var lines = ParseTsv(stdout);
        if (lines is null)
        {
            return OcrResult.Read(stdout);
        }

        // RawText is what the user sees in the UI, so rebuild it from every recognised
        // word, unfiltered. Filtering is the parser's job; hiding text here would make
        // a bad scan impossible to diagnose from the response.
        var rawText = string.Join('\n', lines.Select(l => l.Text));
        return OcrResult.Read(rawText, lines);
    }

    private static List<OcrLine>? ParseTsv(string stdout)
    {
        using var reader = new StringReader(stdout);
        var header = reader.ReadLine();
        if (header is null || !header.StartsWith("level\t", StringComparison.Ordinal))
        {
            return null;
        }

        var columns = header.Split('\t');
        int Column(string name) => Array.IndexOf(columns, name);

        var (levelAt, blockAt, parAt, lineAt) =
            (Column("level"), Column("block_num"), Column("par_num"), Column("line_num"));
        var (topAt, heightAt, confAt, textAt) =
            (Column("top"), Column("height"), Column("conf"), Column("text"));
        if (levelAt < 0 || blockAt < 0 || parAt < 0 || lineAt < 0
            || topAt < 0 || heightAt < 0 || confAt < 0 || textAt < 0)
        {
            return null;
        }

        // Ordered by first appearance so reading order survives for engines/pages where
        // height is uninformative.
        var grouped = new Dictionary<(string Block, string Par, string Line), List<(double Conf, int Height, int Top)>>();
        var texts = new Dictionary<(string Block, string Par, string Line), List<string>>();
        var order = new List<(string Block, string Par, string Line)>();

        while (reader.ReadLine() is { } row)
        {
            var cells = row.Split('\t');
            if (cells.Length <= textAt || cells[levelAt] != "5")
            {
                continue;
            }

            var word = cells[textAt].Trim();
            if (word.Length == 0)
            {
                continue;
            }

            if (!double.TryParse(cells[confAt], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf))
            {
                continue;
            }

            _ = int.TryParse(cells[heightAt], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height);
            _ = int.TryParse(cells[topAt], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top);

            var key = (cells[blockAt], cells[parAt], cells[lineAt]);
            if (!grouped.TryGetValue(key, out var stats))
            {
                grouped[key] = stats = [];
                texts[key] = [];
                order.Add(key);
            }

            stats.Add((conf, height, top));
            texts[key].Add(word);
        }

        return
        [
            .. order.Select(key => new OcrLine(
                string.Join(' ', texts[key]),
                grouped[key].Average(w => w.Conf),
                grouped[key].Max(w => w.Height),
                grouped[key].Min(w => w.Top))),
        ];
    }

    private static string ResolveTessdataPath(OcrOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TessdataPath))
        {
            return options.TessdataPath;
        }

        // TESSDATA_PREFIX is the PARENT of the tessdata dir in our images; append it.
        var prefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        return !string.IsNullOrWhiteSpace(prefix)
            ? Path.Combine(prefix, "tessdata")
            : "/usr/share/tesseract-ocr/5/tessdata";
    }

    // Maps the config language to the traineddata filename stem / `-l` code. Only
    // English is bundled today; an unsupported value warns loudly rather than silently
    // running English (adding a pack here updates both the map and the ceiling).
    private static string ResolveLanguage(string? language, ILogger logger)
    {
        switch (language?.ToLowerInvariant())
        {
            case "eng" or "en" or null or "":
                return "eng";
            default:
                logger.LogWarning(
                    "Ocr:Language '{Language}' is not supported (only English is bundled); falling back to 'eng'.",
                    language);
                return "eng";
        }
    }
}
