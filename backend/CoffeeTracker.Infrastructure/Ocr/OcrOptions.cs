namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>OCR settings, bound from the <c>Ocr</c> configuration section.</summary>
public class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>Which OCR adapter to use: <c>tesseract</c> (default) or <c>none</c> (disabled).</summary>
    public string Engine { get; set; } = "tesseract";

    /// <summary>
    /// Path to the <c>tesseract</c> CLI. When unset, it is resolved from <c>PATH</c>
    /// (the apt package installs it at <c>/usr/bin/tesseract</c>).
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Path to the tessdata directory. When unset, resolved from the
    /// <c>TESSDATA_PREFIX</c> env var (+ <c>/tessdata</c>), then a system default.
    /// </summary>
    public string? TessdataPath { get; set; }

    /// <summary>Tesseract language code (default <c>eng</c>).</summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Hard ceiling, in seconds, on a single OCR run. Bounds a hung, spinning, or
    /// pathological-image tesseract process so it can't pin a worker/CPU on the
    /// single instance. A timeout degrades the scan to "unavailable" (503), distinct
    /// from a genuine caller cancellation.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of tesseract processes allowed to run concurrently. Excess
    /// scan requests queue (honouring their cancellation token) rather than all
    /// spawning native processes at once and exhausting CPU/RAM. A value &lt;= 0
    /// resolves to a default of <c>2 ×</c> the processor count at startup.
    /// </summary>
    public int MaxConcurrency { get; set; }
}
