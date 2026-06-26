using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for the snap-to-fill scan. Orchestrates photo storage,
/// OCR, and label parsing. The HTTP controller depends only on this.
/// </summary>
public interface ICoffeeScanService
{
    Task<ScanResult> ScanAsync(Stream image, string? contentType, long length, CancellationToken ct = default);
}

/// <summary>Outcome of a scan. Expected failures are data, not exceptions.</summary>
public enum ScanStatus
{
    Success,
    InvalidContentType,
    TooLarge,
    /// <summary>OCR is disabled (Ocr:Engine=none) or its engine couldn't run.</summary>
    OcrUnavailable,
}

/// <summary>Result of a scan. <see cref="Response"/> is non-null only on success.</summary>
public sealed record ScanResult(ScanStatus Status, ScanResponseDto? Response);
