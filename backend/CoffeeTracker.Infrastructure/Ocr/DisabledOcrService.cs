using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// OCR adapter used when <c>Ocr:Engine=none</c> — e.g. on the macOS host where the
/// native Tesseract libs aren't installed. Always reports unavailable so the scan
/// endpoint returns 503 and the rest of the app runs normally.
/// </summary>
public class DisabledOcrService : IOcrService
{
    public bool IsAvailable => false;

    public Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default) =>
        Task.FromResult(OcrResult.Unavailable);
}
