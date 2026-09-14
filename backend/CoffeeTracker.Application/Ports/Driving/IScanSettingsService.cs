using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving port for the administrator's view of scanning: which OCR engine the instance
/// uses, and which ones this build could use.
/// </summary>
public interface IScanSettingsService
{
    Task<ScanSettingsDto> GetAsync(CancellationToken ct = default);

    Task<ScanSettingsDto> UpdateAsync(Ports.Driven.OcrEngine engine, CancellationToken ct = default);
}
