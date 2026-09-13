using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service for snap-to-fill: checks the uploaded photo is a real image, OCRs
/// it, and parses the text into best-effort fields. Creates no coffee and keeps no file.
///
/// The bytes are validated but never written. Scanning and saving are separate requests
/// and the client uploads the photo again when it saves, so a file kept here was claimed
/// by nothing — on every scan, not only an abandoned one. Keeping it grew the photos
/// directory with the same image the save was about to store a second time, and left the
/// admin cleanup as the only thing bounding it.
///
/// Validation still runs in full: an OCR process should not be handed a decompression
/// bomb or a file that merely claims to be an image.
/// </summary>
public class CoffeeScanService(
    IOcrService ocr,
    IPhotoStorage photoStorage,
    ICoffeeLabelParser parser) : ICoffeeScanService
{
    public async Task<ScanResult> ScanAsync(Stream image, string? contentType, long length, CancellationToken ct = default)
    {
        // Short-circuit before any work when OCR can't run here (e.g. the host with
        // no native Tesseract libs) — endpoint maps this to 503.
        if (!ocr.IsAvailable)
        {
            return new ScanResult(ScanStatus.OcrUnavailable, null);
        }

        // Buffer once so the same bytes feed both validation and OCR.
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);

        // Validate first: cheaply rejects non-images before the expensive OCR run.
        buffer.Position = 0;
        var accepted = await photoStorage.ValidateAsync(buffer, contentType, buffer.Length, ct);
        if (accepted != PhotoStorageStatus.Stored)
        {
            return new ScanResult(MapRejection(accepted), null);
        }

        buffer.Position = 0;
        var result = await ocr.ReadAsync(buffer, ct);
        if (!result.Available)
        {
            return new ScanResult(ScanStatus.OcrUnavailable, null);
        }

        return new ScanResult(ScanStatus.Success, new ScanResponseDto(result.RawText, parser.Parse(result)));
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
