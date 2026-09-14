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

/// <summary>
/// One recognised line of text, with the engine's own quality signals where it
/// reports them.
/// </summary>
/// <param name="Text">The line's text.</param>
/// <param name="Confidence">
/// Mean per-word confidence, 0-100, or null when the engine doesn't report it.
/// This is what separates label text from background noise: on a real photo of a
/// bag on a table, the noise lines score well under 50 while the printed label
/// scores 60+.
/// </param>
/// <param name="Height">
/// Glyph height in pixels, or null when unknown. A proxy for how physically
/// prominent the text is on the bag, which is how the brand/product name is told
/// apart from the small print around it.
///
/// It is a bounding box, so it is not font size: a line carrying a descender
/// measures taller than the same type without one. "Injerto" comes back 25% taller
/// than "Finca El" set in the identical face, which is why height is used to group
/// lines rather than to order them one against the next.
/// </param>
/// <param name="Top">
/// Distance in pixels from the top of the image to the line's bounding box, or null
/// when the engine reports no geometry. Only meaningful next to <paramref name="Height"/>:
/// the two together are what say whether a line is the continuation of the one above
/// it or a different piece of the label entirely.
/// </param>
public sealed record OcrLine(string Text, double? Confidence, int? Height, int? Top = null);

public sealed record OcrResult(bool Available, string RawText, IReadOnlyList<OcrLine> Lines)
{
    public static OcrResult Unavailable { get; } = new(false, string.Empty, []);

    /// <summary>
    /// A read from an engine that reports only text. Lines carry no confidence or
    /// geometry, so downstream ranking falls back to reading order.
    /// </summary>
    public static OcrResult Read(string rawText) =>
        new(true, rawText, SplitLines(rawText));

    /// <summary>A read from an engine that reports per-line quality signals.</summary>
    public static OcrResult Read(string rawText, IReadOnlyList<OcrLine> lines) =>
        new(true, rawText, lines);

    private static List<OcrLine> SplitLines(string rawText) =>
        [.. (rawText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .Select(l => new OcrLine(l, null, null))];
}
