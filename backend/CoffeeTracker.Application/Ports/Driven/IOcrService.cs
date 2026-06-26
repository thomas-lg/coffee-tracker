namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Driven (output) port for optical character recognition. Implemented by a
/// swappable adapter (Tesseract today; PaddleOCR/RapidOCR later) selected via
/// configuration, or a disabled adapter when the native engine isn't present.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Whether OCR is usable in this environment. Lets callers short-circuit (and
    /// the scan endpoint return 503) without doing any work when OCR is disabled.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Extracts text from an image. Returns <see cref="OcrResult.Unavailable"/>
    /// (never throws out) if the engine can't run, so a failure degrades to 503
    /// rather than a 500.
    /// </summary>
    Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default);
}

/// <summary>Outcome of an OCR read.</summary>
public sealed record OcrResult(bool Available, string RawText)
{
    public static OcrResult Unavailable { get; } = new(false, string.Empty);

    public static OcrResult Read(string rawText) => new(true, rawText);
}
