using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service implementing the catalog driving port. Orchestrates the
/// repository port and maps domain entities to response DTOs.
/// </summary>
public class CoffeeCatalogService(ICoffeeRepository repository) : ICoffeeCatalogService
{
    public async Task<IReadOnlyList<CoffeeResponseDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var coffees = await repository.GetAllAsync(ct);
        return coffees.Select(ToDto).ToList();
    }

    private static CoffeeResponseDto ToDto(Coffee c) => new(
        c.Id,
        c.Name,
        c.Roaster,
        c.Origin,
        c.RoastLevel,
        c.Price,
        c.DateBought,
        c.PhotoPath,
        c.ShopName,
        c.PurchaseUrl,
        c.CreatedAt);
}
