namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Shape returned to clients for a coffee. Keeps the domain entity off the wire
/// so storage and API contract can evolve independently.
/// </summary>
public record CoffeeResponseDto(
    int Id,
    string Name,
    string Roaster,
    string Origin,
    string RoastLevel,
    decimal Price,
    DateOnly DateBought,
    string? PhotoPath,
    string? ShopName,
    string? PurchaseUrl,
    DateTimeOffset CreatedAt);
