using System.ComponentModel.DataAnnotations;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Payload for creating a coffee. Field-level rules are enforced by the
/// ASP.NET Core model-binding pipeline before the request reaches the service.
/// Attributes target the positional record's constructor parameters (no
/// <c>property:</c> prefix), which is what MVC validation reads.
/// </summary>
public record CoffeeCreateDto(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, StringLength(200, MinimumLength = 1)] string Roaster,
    [Required, StringLength(200, MinimumLength = 1)] string Origin,
    [Required, EnumDataType(typeof(RoastLevel))] RoastLevel? RoastLevel,
    [Range(0, 100_000)] decimal Price,
    DateOnly DateBought,
    [StringLength(200)] string? ShopName,
    [Url, StringLength(2000)] string? PurchaseUrl) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DtoValidation.ValidateDateBought(DateBought, nameof(DateBought), validationContext);
}
