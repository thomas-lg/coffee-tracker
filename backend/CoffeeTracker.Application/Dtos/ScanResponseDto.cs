namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Result of scanning a bag photo: the raw OCR text (so the client can show/refine
/// it), the parsed best-effort fields, and the stored photo path so the eventual
/// coffee can reuse the same image.
/// </summary>
public record ScanResponseDto(
    string RawText,
    ScannedCoffeeDto Parsed,
    string PhotoPath);
