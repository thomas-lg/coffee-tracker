using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// Answers which engines can actually run on this host, by asking each adapter's own
/// availability check rather than re-deriving it.
/// </summary>
public sealed class OcrEngineCatalogue(RapidOcrService rapid, TesseractCliOcrService tesseract)
    : IOcrEngineCatalogue
{
    public IReadOnlyList<ScanEngineOptionDto> Options() =>
    [
        new(OcrEngine.RapidOcr, rapid.IsAvailable),
        new(OcrEngine.Tesseract, tesseract.IsAvailable),
        // Turning scanning off needs nothing installed, so it is always on offer.
        new(OcrEngine.Disabled, true),
    ];
}
