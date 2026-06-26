using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Payload to create a review. Rating is validated 1–5 by the model-binding
/// pipeline; <see cref="TagIds"/> references existing flavor tags by id.
/// </summary>
public record ReviewCreateDto(
    [Range(1, 5)] int Rating,
    [StringLength(2000)] string? TastingNotes,
    [StringLength(100)] string? BrewMethod,
    [StringLength(100)] string? Grind,
    [StringLength(50)] string? Ratio,
    IReadOnlyList<int>? TagIds);
