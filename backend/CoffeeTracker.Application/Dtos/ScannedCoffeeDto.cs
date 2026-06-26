namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Best-effort fields parsed from a coffee-bag label. Every field is optional —
/// the client pre-fills the Add Coffee form with whatever was recognized and the
/// user corrects the rest.
/// </summary>
public record ScannedCoffeeDto(
    string? Name,
    string? Roaster,
    string? Origin,
    string? RoastLevel,
    string? Weight);
