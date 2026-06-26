using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for reading the coffee catalog. Driving adapters
/// (e.g. the HTTP controller) depend only on this abstraction.
/// </summary>
public interface ICoffeeCatalogService
{
    Task<IReadOnlyList<CoffeeResponseDto>> GetCatalogAsync(CancellationToken ct = default);
}
