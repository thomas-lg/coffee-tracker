using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Turns raw OCR text from a coffee bag into best-effort structured fields. Pure
/// (no I/O / native deps), so it's the unit-tested core of the scan feature.
/// </summary>
public interface ICoffeeLabelParser
{
    ScannedCoffeeDto Parse(string rawText);
}
