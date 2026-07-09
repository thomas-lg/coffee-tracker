namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Result of scanning a bag photo: the raw OCR text (so the client can show/refine
/// it), the parsed best-effort fields, and a signed URL to preview the stored image.
/// </summary>
public record ScanResponseDto(
    string RawText,
    ScannedCoffeeDto Parsed,
    string PhotoUrl);
