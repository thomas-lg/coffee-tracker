namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Result of scanning a bag photo: the raw OCR text (so the client can show or refine
/// it) and the parsed best-effort fields.
///
/// No photo URL: the scan keeps no file. The client already holds the image it picked
/// and uploads it when the coffee is saved.
/// </summary>
public record ScanResponseDto(
    string RawText,
    ScannedCoffeeDto Parsed);
