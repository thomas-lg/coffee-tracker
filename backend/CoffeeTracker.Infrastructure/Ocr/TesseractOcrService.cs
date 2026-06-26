using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TesseractOCR;
using TesseractOCR.Enums;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// OCR adapter over the TesseractOCR NuGet (P/Invokes native libtesseract /
/// libleptonica — present in the dev container and prod image). Any failure is
/// caught and reported as unavailable so a runtime problem degrades to 503 rather
/// than a 500, and never takes the app down at startup.
/// </summary>
public class TesseractOcrService(IOptions<OcrOptions> options, ILogger<TesseractOcrService> logger) : IOcrService
{
    private readonly string _tessdataPath = ResolveTessdataPath(options.Value);
    private readonly string _languageCode = NormalizeLanguageCode(options.Value.Language);
    private readonly Language _language = MapLanguage(options.Value.Language);

    // Cheap, side-effect-free check: the engine can only load if the language's
    // traineddata file is actually present, so check the file (not just the dir) —
    // a present-but-empty tessdata mount shouldn't report available and then fail
    // after we've already stored the photo.
    public bool IsAvailable => File.Exists(Path.Combine(_tessdataPath, $"{_languageCode}.traineddata"));

    public async Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default)
    {
        try
        {
            // Avoid an extra copy: the caller passes a buffered MemoryStream, and
            // Pix.Image needs a byte[]. Read it directly when we can.
            byte[] bytes;
            if (image is MemoryStream ms)
            {
                bytes = ms.ToArray();
            }
            else
            {
                using var buffer = new MemoryStream();
                await image.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }

            // The Tesseract engine isn't safe for concurrent Process calls and
            // snap-to-fill is infrequent, so build a fresh engine per request.
            using var engine = new Engine(_tessdataPath, _language, EngineMode.Default);
            using var pix = TesseractOCR.Pix.Image.LoadFromMemory(bytes);
            using var page = engine.Process(pix);
            return OcrResult.Read(page.Text ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tesseract OCR failed; reporting unavailable.");
            return OcrResult.Unavailable;
        }
    }

    private static string ResolveTessdataPath(OcrOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TessdataPath))
        {
            return options.TessdataPath;
        }

        // TESSDATA_PREFIX is the PARENT of the tessdata dir; Tesseract appends it.
        var prefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        return !string.IsNullOrWhiteSpace(prefix)
            ? Path.Combine(prefix, "tessdata")
            : "/usr/share/tesseract-ocr/5/tessdata";
    }

    private static Language MapLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "eng" or "en" or null or "" => Language.English,
        // M5 standardizes on English; other packs are a future addition.
        _ => Language.English,
    };

    // The tessdata filename stem for the configured language (matches MapLanguage).
    private static string NormalizeLanguageCode(string? language) => language?.ToLowerInvariant() switch
    {
        "eng" or "en" or null or "" => "eng",
        _ => "eng",
    };
}
