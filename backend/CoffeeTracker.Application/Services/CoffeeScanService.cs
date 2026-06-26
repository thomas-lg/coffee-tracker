using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service for snap-to-fill: stores the uploaded photo, OCRs it, and
/// parses the text into best-effort fields. Does not create a coffee.
/// </summary>
public class CoffeeScanService(
    IOcrService ocr,
    IPhotoStorage photoStorage,
    ICoffeeLabelParser parser) : ICoffeeScanService
{
    public async Task<ScanResult> ScanAsync(Stream image, string? contentType, long length, CancellationToken ct = default)
    {
        // Short-circuit before any work when OCR can't run here (e.g. the host with
        // no native Tesseract libs) — no photo stored, endpoint maps this to 503.
        if (!ocr.IsAvailable)
        {
            return new ScanResult(ScanStatus.OcrUnavailable, null);
        }

        // Buffer once so the same bytes feed both photo storage and OCR.
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);

        // Validate + store first: cheaply rejects non-images before expensive OCR,
        // and retains the photo so the eventual coffee can reuse it.
        buffer.Position = 0;
        var stored = await photoStorage.SaveAsync(buffer, contentType, buffer.Length, ct);
        if (stored.Status != PhotoStorageStatus.Stored)
        {
            return new ScanResult(MapRejection(stored.Status), null);
        }

        buffer.Position = 0;
        var result = await ocr.ReadAsync(buffer, ct);
        if (!result.Available)
        {
            // Engine reported available but failed mid-read; don't orphan the file.
            await photoStorage.DeleteAsync(stored.RelativePath!, ct);
            return new ScanResult(ScanStatus.OcrUnavailable, null);
        }

        var parsed = parser.Parse(result.RawText);
        return new ScanResult(ScanStatus.Success, new ScanResponseDto(result.RawText, parsed, stored.RelativePath!));
    }

    private static ScanStatus MapRejection(PhotoStorageStatus status) => status switch
    {
        PhotoStorageStatus.InvalidContentType => ScanStatus.InvalidContentType,
        PhotoStorageStatus.TooLarge => ScanStatus.TooLarge,
        // Stored isn't a rejection; any new status must be mapped deliberately
        // rather than silently masquerading as an invalid content type.
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unexpected photo storage rejection."),
    };
}
