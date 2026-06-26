namespace CoffeeTracker.Application.Dtos;

/// <summary>Shape returned to clients for a review, including its flavor tags.</summary>
public record ReviewResponseDto(
    int Id,
    int CoffeeId,
    string UserId,
    int Rating,
    string? TastingNotes,
    string? BrewMethod,
    string? Grind,
    string? Ratio,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<FlavorTagDto> Tags);
