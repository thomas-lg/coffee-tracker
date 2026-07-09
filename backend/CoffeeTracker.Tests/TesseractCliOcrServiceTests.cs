using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// IsAvailable + tessdata-path resolution are pure (no engine call), so they're
// testable on CI. Real OCR (ReadAsync → the tesseract CLI) runs in the container/prod;
// here we only assert it degrades gracefully when the binary is absent.
// One case mutates the process-global TESSDATA_PREFIX. Tagging this class into a named
// collection serializes it against any *other* class that opts into the same
// collection — add that tag to future tests that read TESSDATA_PREFIX to avoid races.
[Collection("env-mutating")]
public class TesseractCliOcrServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ct-ocr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static TesseractCliOcrService NewService(OcrOptions options) =>
        new(Options.Create(options), NullLogger<TesseractCliOcrService>.Instance);

    [Fact]
    public async Task ReadAsync_ReportsUnavailable_WhenExecutableMissing()
    {
        // traineddata present (IsAvailable true) but a bogus binary path — ReadAsync
        // must degrade to unavailable rather than throw, so the endpoint returns 503.
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "eng.traineddata"), "x");
        var service = NewService(new OcrOptions
        {
            TessdataPath = _tempDir,
            ExecutablePath = "/nonexistent/tesseract-not-here",
        });

        Assert.True(service.IsAvailable);
        var result = await service.ReadAsync(new MemoryStream([1, 2, 3]));
        Assert.False(result.Available);
    }

    /// <summary>
    /// Writes an executable stub standing in for the tesseract CLI (Unix only). The
    /// stub drains stdin (as the adapter writes the image there) then runs
    /// <paramref name="body"/>. Lets the timeout/concurrency plumbing be exercised
    /// without a real OCR engine.
    /// </summary>
    private string WriteStubExecutable(string body)
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "eng.traineddata"), "x");
        var script = Path.Combine(_tempDir, "stub-tesseract.sh");
        File.WriteAllText(script, "#!/bin/sh\ncat > /dev/null\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return script;
    }

    [Fact]
    public async Task ReadAsync_ReturnsStdout_FromTheEngine()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // sh stub — suite runs on macOS/Linux
        }

        var service = NewService(new OcrOptions
        {
            TessdataPath = _tempDir,
            ExecutablePath = WriteStubExecutable("printf 'Hello OCR'"),
        });

        var result = await service.ReadAsync(new MemoryStream([1, 2, 3]));

        Assert.True(result.Available);
        Assert.Equal("Hello OCR", result.RawText);
    }

    [Fact]
    public async Task ReadAsync_KillsAHungEngine_AndReportsUnavailable_OnTimeout()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The stub hangs far beyond the 1s configured ceiling; the adapter must give
        // up on its own timeout (NOT the caller's ct) and degrade to unavailable.
        var service = NewService(new OcrOptions
        {
            TessdataPath = _tempDir,
            ExecutablePath = WriteStubExecutable("sleep 300\nprintf 'too late'"),
            TimeoutSeconds = 1,
        });

        var started = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.ReadAsync(new MemoryStream([1, 2, 3]));
        started.Stop();

        Assert.False(result.Available); // timeout → engine outage, not an exception
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(30),
            $"timed-out run should return promptly, took {started.Elapsed}");
    }

    [Fact]
    public async Task ReadAsync_CapsConcurrentEngineProcesses_ToMaxConcurrency()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The stub takes an atomic mkdir lock: if two stub processes ever run at the
        // same time the second fails the mkdir and prints OVERLAP. With
        // MaxConcurrency=1 the semaphore must serialize the runs, so every result
        // reads "ok". Removing the gate makes this fail with OVERLAP outputs.
        var lockDir = Path.Combine(_tempDir, "concurrency-lock");
        var service = NewService(new OcrOptions
        {
            TessdataPath = _tempDir,
            ExecutablePath = WriteStubExecutable(
                $"if mkdir \"{lockDir}\" 2>/dev/null; then sleep 0.3; rmdir \"{lockDir}\"; printf ok; else printf OVERLAP; fi"),
            MaxConcurrency = 1,
        });

        var runs = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => Task.Run(() => service.ReadAsync(new MemoryStream([1, 2, 3])))));

        Assert.All(runs, r =>
        {
            Assert.True(r.Available);
            Assert.Equal("ok", r.RawText);
        });
    }

    [Fact]
    public void IsAvailable_True_WhenLanguageTraineddataPresent()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "eng.traineddata"), "x");

        var service = NewService(new OcrOptions { TessdataPath = _tempDir, Language = "eng" });

        Assert.True(service.IsAvailable);
    }

    [Fact]
    public void IsAvailable_False_WhenTessdataDirExistsButTraineddataMissing()
    {
        Directory.CreateDirectory(_tempDir); // dir present, but no eng.traineddata

        var service = NewService(new OcrOptions { TessdataPath = _tempDir });

        Assert.False(service.IsAvailable);
    }

    [Fact]
    public void IsAvailable_False_WhenPathMissing()
    {
        var service = NewService(new OcrOptions { TessdataPath = _tempDir }); // never created

        Assert.False(service.IsAvailable);
    }

    [Fact]
    public void ResolvesTessdataPath_FromTessdataPrefix_WhenNoExplicitPath()
    {
        // TESSDATA_PREFIX is the PARENT; the adapter appends /tessdata.
        var tessdata = Path.Combine(_tempDir, "tessdata");
        Directory.CreateDirectory(tessdata);
        File.WriteAllText(Path.Combine(tessdata, "eng.traineddata"), "x");

        var prev = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        try
        {
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", _tempDir);
            var service = NewService(new OcrOptions { TessdataPath = null });
            Assert.True(service.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", prev);
        }
    }
}
