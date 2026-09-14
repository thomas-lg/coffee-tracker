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
    /// Tesseract page segmentation mode (<c>--psm</c>). Default 11, "sparse text: find
    /// as much text as possible in no particular order".
    /// </summary>
    /// <remarks>
    /// Swept three times, and the third overturned the first two.
    ///
    /// Against rendered fixtures, mode 6 ("a single uniform block of text") looked worth
    /// five points over Tesseract's default of 3. Then the parser stopped truncating
    /// wrapped lines and every mode tied at 81.3%. Mode 6 had been papering over a parser
    /// defect rather than reading better. Then real photographs joined the corpus, and
    /// mode 6 came last of the five on them: 32% on a handheld shot where 11 scores 54%,
    /// and 40% on matte-black packaging where 11 scores 60%.
    ///
    /// The reason is the assumption in its name. A rendered fixture is a bag filling the
    /// frame on a plain ground, so "one uniform block" is nearly true. A photograph is a
    /// bag on a kitchen table, and mode 6 reads the table as part of the block: it
    /// returned 299 lines for a label carrying about 12, and the parser handed back names
    /// like "| 1 an A | TORREFACTION MEDIUM #7". Mode 11 looks for text wherever it sits
    /// and ignores the rest, which is the right question to ask of a photograph.
    ///
    /// So re-sweep after changing anything the modes interact with, and weight the
    /// photographs when you read the result: each corpus scores highest on the pipeline
    /// tuned for images like its own.
    /// </remarks>
    public int Psm { get; set; } = 11;

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
