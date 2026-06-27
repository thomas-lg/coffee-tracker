using System.Diagnostics;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// OCR adapter that shells out to the system <c>tesseract</c> CLI (installed via apt
/// in the dev container and the prod image). We use the CLI rather than a P/Invoke
/// NuGet because the latter's native-library loading on Linux is brittle — it probes
/// version-pinned <c>lib*.dll.so</c> names that no distro ships and needs a <c>libdl</c>
/// shim on modern glibc. The CLI is Tesseract's first-class, stable interface.
/// </summary>
public class TesseractCliOcrService(IOptions<OcrOptions> options, ILogger<TesseractCliOcrService> logger) : IOcrService
{
    private readonly string _executable =
        string.IsNullOrWhiteSpace(options.Value.ExecutablePath) ? "tesseract" : options.Value.ExecutablePath!;
    private readonly string _tessdataPath = ResolveTessdataPath(options.Value);
    private readonly string _language = ResolveLanguage(options.Value.Language);

    // Cheap, side-effect-free check: the engine can only run if the language's
    // traineddata is present. A missing `tesseract` binary is handled in ReadAsync
    // (the process fails to start and we degrade to unavailable), so this stays a
    // pure file check — no process spawn just to test availability.
    public bool IsAvailable => File.Exists(Path.Combine(_tessdataPath, $"{_language}.traineddata"));

    public async Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default)
    {
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

            process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Could not start '{_executable}'.");

            // Start draining stdout/stderr before writing stdin so a large image can't
            // deadlock against a full output pipe.
            stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            stderrTask = process.StandardError.ReadToEndAsync(ct);

            await image.CopyToAsync(process.StandardInput.BaseStream, ct);
            process.StandardInput.Close();

            await process.WaitForExitAsync(ct);
            // Observe both pipes (stderr too, even on success) so neither read is left
            // as an unobserved task.
            var text = await stdoutTask;
            var error = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning("tesseract exited {ExitCode}: {Error}", process.ExitCode, error);
                return OcrResult.Unavailable;
            }

            return OcrResult.Read(text);
        }
        catch (OperationCanceledException)
        {
            // The caller cancelled — propagate rather than masquerading as an engine
            // outage (which would log noise and skew availability signals).
            throw;
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
                // Dispose() does NOT terminate the child; on cancel/error the process
                // (possibly blocked waiting for stdin EOF) would otherwise leak. Kill the
                // tree, then observe the pipe reads (the kill unblocks them) before dispose.
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Raced with exit — nothing to kill.
                }

                await Observe(stdoutTask);
                await Observe(stderrTask);
                process.Dispose();
            }
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
            // Already handled by the caller's catch, or cancelled — ignore here.
        }
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

    // Maps the config language to the traineddata filename stem / `-l` code. M5
    // standardizes on English; adding a pack here updates both at once.
    private static string ResolveLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "eng" or "en" or null or "" => "eng",
        _ => "eng",
    };
}
