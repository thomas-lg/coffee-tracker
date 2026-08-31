using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Turns raw OCR text from a coffee bag into best-effort structured fields. Pure
/// (no I/O / native deps), so it's the unit-tested core of the scan feature.
/// </summary>
public interface ICoffeeLabelParser
{
    /// <summary>
    /// Parses an OCR read. Prefer this overload: the engine's per-line confidence and
    /// glyph height are what let the parser ignore background noise and find the text
    /// that is actually printed large on the bag.
    /// </summary>
    ScannedCoffeeDto Parse(OcrResult ocr);

    /// <summary>
    /// Text-only parse, for engines (or tests) with no quality signals. Falls back to
    /// reading order, which is materially worse on photos — the top of a photo is
    /// usually background, not the product name.
    /// </summary>
    ScannedCoffeeDto Parse(string rawText);
}
