using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// Answers which engines can actually run on this host, by asking each adapter's own
/// availability check rather than re-deriving it.
/// </summary>
public sealed class OcrEngineCatalogue(
    [FromKeyedServices(OcrEngine.RapidOcr)] IOcrService rapid,
    [FromKeyedServices(OcrEngine.Tesseract)] IOcrService tesseract) : IOcrEngineCatalogue
{
    public async Task<IReadOnlyList<ScanEngineOptionDto>> OptionsAsync(CancellationToken ct = default) =>
    [
        new(OcrEngine.RapidOcr, await rapid.IsAvailableAsync(ct)),
        new(OcrEngine.Tesseract, await tesseract.IsAvailableAsync(ct)),
        // Turning scanning off needs nothing installed, so it is always on offer.
        new(OcrEngine.Disabled, true),
    ];
}
