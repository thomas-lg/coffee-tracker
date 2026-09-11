using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>The ID token the provider returned, presented for an app session.</summary>
public record OidcSignInDto([Required, StringLength(8192, MinimumLength = 1)] string IdToken);
