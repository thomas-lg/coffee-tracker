using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>Payload to update a review. Same shape as create; the id comes from the route.</summary>
public record ReviewUpdateDto(
    [Range(1, 5)] int Rating,
    [StringLength(2000)] string? TastingNotes,
    [StringLength(100)] string? BrewMethod,
    [StringLength(100)] string? Grind,
    [StringLength(50)] string? Ratio,
    IReadOnlyList<int>? TagIds);
