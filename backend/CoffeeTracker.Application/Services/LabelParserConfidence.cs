namespace CoffeeTracker.Application.Services;

/// <summary>
/// The confidence gate the label parser should use, supplied by whoever registered the
/// OCR engine.
///
/// The gate is a property of how an engine scores, not of the bag: Tesseract's noise
/// lines land under 50 and its real text above 58, while RapidOCR scores the same noise
/// around 55 and the same text above 87. One number cannot serve both, and the
/// application layer has no business knowing which engine is plugged in, so the engine's
/// registration says.
/// </summary>
/// <param name="Gate">Mean per-line confidence, 0-100, below which a line is noise.</param>
public sealed record LabelParserConfidence(double Gate);
