using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// The <see cref="IOcrService"/> the app actually resolves. It holds both engines and
/// asks the stored policy which one to use, per scan.
///
/// Both engines stay singletons because each caps its own concurrency with a semaphore,
/// and a per-request copy would cap nothing. This wrapper is the scoped part, since
/// reading the policy needs the request's DbContext.
/// </summary>
public sealed class SwitchingOcrService(
    IOcrEnginePolicy policy,
    RapidOcrService rapid,
    TesseractCliOcrService tesseract,
    DisabledOcrService disabled) : IOcrService
{
    /// <summary>
    /// Whether *some* engine could run here, which is deliberately weaker than "the
    /// chosen one can". The port's check is synchronous and the choice lives in the
    /// database, so answering precisely would mean blocking on a query in a property.
    /// Nothing is lost: <see cref="ReadAsync"/> returns
    /// <see cref="OcrResult.Unavailable"/> when the chosen engine cannot run, and the
    /// scan endpoint maps both that and this to the same 503.
    /// </summary>
    public bool IsAvailable => rapid.IsAvailable || tesseract.IsAvailable;

    public async Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default)
    {
        var engine = await policy.GetAsync(ct);
        return await Selected(engine).ReadAsync(image, ct);
    }

    /// <summary>Which adapter an engine names. Public so the admin surface can ask.</summary>
    public IOcrService Selected(OcrEngine engine) => engine switch
    {
        OcrEngine.Tesseract => tesseract,
        OcrEngine.Disabled => disabled,
        _ => rapid,
    };
}
