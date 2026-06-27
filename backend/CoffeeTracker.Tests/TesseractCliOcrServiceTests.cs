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
