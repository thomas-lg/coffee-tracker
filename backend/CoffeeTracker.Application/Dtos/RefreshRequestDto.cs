using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>Body for token refresh and logout: the opaque refresh token previously issued.</summary>
public record RefreshRequestDto(
    [Required, StringLength(4096)] string RefreshToken);
