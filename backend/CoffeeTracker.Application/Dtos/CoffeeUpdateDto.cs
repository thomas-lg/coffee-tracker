using System.ComponentModel.DataAnnotations;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Payload for updating a coffee. Same mutable fields as create; the id comes
/// from the route, not the body. Attributes target the positional record's
/// constructor parameters (no <c>property:</c> prefix).
/// </summary>
public record CoffeeUpdateDto(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, StringLength(200, MinimumLength = 1)] string Roaster,
    [Required, StringLength(200, MinimumLength = 1)] string Origin,
    [EnumDataType(typeof(RoastLevel))] RoastLevel RoastLevel,
    [Range(0, 100_000)] decimal Price,
    DateOnly DateBought,
    [StringLength(200)] string? ShopName,
    [Url, StringLength(2000)] string? PurchaseUrl);
