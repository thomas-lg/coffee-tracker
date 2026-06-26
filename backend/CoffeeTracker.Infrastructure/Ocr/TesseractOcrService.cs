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
    // One resolution of the language so the engine's Language and the traineddata
    // filename stem can never drift apart (a mismatch would make IsAvailable check
    // the wrong file and then fail at load).
    private readonly (Language Tesseract, string Code) _language = ResolveLanguage(options.Value.Language);

    // Cheap, side-effect-free check: the engine can only load if the language's
    // traineddata file is actually present, so check the file (not just the dir) —
    // a present-but-empty tessdata mount shouldn't report available and then fail
    // after we've already stored the photo.
    public bool IsAvailable => File.Exists(Path.Combine(_tessdataPath, $"{_language.Code}.traineddata"));

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
            using var engine = new Engine(_tessdataPath, _language.Tesseract, EngineMode.Default);
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

    // Single source of truth: maps the config language to BOTH the Tesseract enum
    // and the traineddata filename stem. M5 standardizes on English; adding a pack
    // here updates the engine language and the availability check together.
    private static (Language Tesseract, string Code) ResolveLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        "eng" or "en" or null or "" => (Language.English, "eng"),
        _ => (Language.English, "eng"),
    };
}
