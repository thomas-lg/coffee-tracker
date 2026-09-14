using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Reads and writes the chosen OCR engine from the singleton <see cref="AppSettings"/>
/// row, falling back to the configured default while nobody has chosen.
/// </summary>
public sealed class EfOcrEnginePolicy(AppDbContext db, IOptions<OcrOptions> options) : IOcrEnginePolicy
{
    public async Task<OcrEngine> GetAsync(CancellationToken ct = default)
    {
        var stored = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Id == AppSettings.SingletonId)
            .Select(s => s.OcrEngine)
            .FirstOrDefaultAsync(ct);

        // An unrecognised string falls through to the configured default rather than
        // disabling scanning: the value can only get there by hand, and silently
        // switching the feature off would be the worst way to report a typo.
        return Parse(stored) ?? Parse(options.Value.Engine) ?? OcrEngine.RapidOcr;
    }

    public async Task SetAsync(OcrEngine engine, CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Id == AppSettings.SingletonId, ct);
        if (row is null)
        {
            row = new AppSettings { Id = AppSettings.SingletonId };
            db.AppSettings.Add(row);
        }

        row.OcrEngine = engine.ToString();
        await db.SaveChangesAsync(ct);
    }

    private static OcrEngine? Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "rapidocr" => OcrEngine.RapidOcr,
        "tesseract" => OcrEngine.Tesseract,
        "none" or "disabled" => OcrEngine.Disabled,
        _ => null,
    };
}
