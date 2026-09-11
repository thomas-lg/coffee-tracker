namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Public client bootstrap config, served anonymously so the SPA can adapt its UI
/// before a user signs in: which sign-in methods to offer, and how to reach the
/// provider when there is one.
/// </summary>
public record ConfigDto(
    bool LocalLoginEnabled,
    bool RegistrationEnabled,
    bool OidcAvailable,
    OidcClientConfigDto? Oidc);

/// <summary>
/// What the browser needs to run the authorization flow. Present only when a provider
/// is configured and reachable. Nothing here is secret.
/// </summary>
public record OidcClientConfigDto(string Authority, string ClientId, string Scopes);
