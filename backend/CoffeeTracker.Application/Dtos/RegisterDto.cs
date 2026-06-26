using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Registration payload. Field-level rules run in the model-binding pipeline;
/// password *strength* is enforced by the Identity password policy in the adapter.
/// </summary>
public record RegisterDto(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required, StringLength(100, MinimumLength = 1)] string DisplayName);
