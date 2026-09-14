using System.Text.Json.Serialization;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Which OCR engine this instance scans with. Persisted and changed at runtime by an
/// administrator, so this is a read/write port rather than bound configuration.
///
/// It used to be configuration, read once at startup. That was fine while there was one
/// engine; with two that differ in accuracy, speed and what they cost the host, the
/// choice belongs to whoever runs the instance and looks at the bags, not to whoever
/// last edited an environment variable.
/// </summary>
public interface IOcrEnginePolicy
{
    /// <summary>
    /// The engine in force. Falls back to the configured default when an administrator
    /// has never chosen, so an instance that is upgraded rather than installed keeps
    /// behaving as its deployment says.
    /// </summary>
    Task<OcrEngine> GetAsync(CancellationToken ct = default);

    Task SetAsync(OcrEngine engine, CancellationToken ct = default);
}

/// <summary>
/// The engines an instance can scan with. A closed set rather than a string, because an
/// unknown value here would silently disable scanning.
///
/// Serialised as its name, not its ordinal, the same way <c>RoastLevel</c> is and for the
/// same reason: the attribute is what makes the generated OpenAPI schema a string enum,
/// so reordering these members cannot repoint a client at a different engine.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OcrEngine>))]
public enum OcrEngine
{
    /// <summary>PP-OCRv6 on onnxruntime. Reads photographs of bags markedly better.</summary>
    RapidOcr,

    /// <summary>The Tesseract CLI. Lighter and faster, better on flat scans.</summary>
    Tesseract,

    /// <summary>Scanning off: the endpoint answers 503 without starting anything.</summary>
    Disabled,
}
