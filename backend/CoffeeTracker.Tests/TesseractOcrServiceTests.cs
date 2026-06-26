using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// IsAvailable + tessdata-path resolution are pure (no native libs), so they're
// testable on CI. The actual OCR (ReadAsync → native Tesseract) is exercised in
// the container/prod, not here.
public class TesseractOcrServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ct-ocr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static TesseractOcrService NewService(OcrOptions options) =>
        new(Options.Create(options), NullLogger<TesseractOcrService>.Instance);

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
