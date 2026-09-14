using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.DependencyInjection;

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
    [FromKeyedServices(OcrEngine.RapidOcr)] IOcrService rapid,
    [FromKeyedServices(OcrEngine.Tesseract)] IOcrService tesseract,
    [FromKeyedServices(OcrEngine.Disabled)] IOcrService disabled) : IOcrService
{
    /// <summary>Whether the engine actually in force can run here.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default) =>
        await Selected(await policy.GetAsync(ct)).IsAvailableAsync(ct);

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
