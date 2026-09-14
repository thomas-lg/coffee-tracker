using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Reads and changes the engine snap-to-fill scans with.
///
/// There is no "would break scanning" refusal here, unlike the account policy's
/// lock-everyone-out guard. Choosing an engine that cannot run costs a 503 on the next
/// scan and is undone by choosing another; nobody is shut out of the instance. What the
/// administrator gets instead is the availability of each option, so the choice is
/// informed rather than refused after the fact.
/// </summary>
public class ScanSettingsService(IOcrEnginePolicy policy, IOcrEngineCatalogue catalogue)
    : IScanSettingsService
{
    public async Task<ScanSettingsDto> GetAsync(CancellationToken ct = default) =>
        new(await policy.GetAsync(ct), await catalogue.OptionsAsync(ct));

    public async Task<ScanSettingsDto> UpdateAsync(OcrEngine engine, CancellationToken ct = default)
    {
        await policy.SetAsync(engine, ct);
        return await GetAsync(ct);
    }
}
