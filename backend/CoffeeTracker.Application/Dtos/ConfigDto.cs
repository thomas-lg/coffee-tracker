namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Public client bootstrap config, served anonymously so the SPA can adapt its UI
/// before a user signs in: which sign-in methods to offer, and whether to show the
/// register option.
/// </summary>
public record ConfigDto(
    bool LocalLoginEnabled,
    bool RegistrationEnabled,
    bool OidcAvailable);
