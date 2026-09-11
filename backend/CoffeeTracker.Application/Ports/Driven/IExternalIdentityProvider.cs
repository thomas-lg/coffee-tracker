namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// The external OpenID Connect provider, when one is configured. Deployments without
/// one get an implementation that reports itself unavailable, so the rest of the app
/// never branches on configuration.
/// </summary>
public interface IExternalIdentityProvider
{
    /// <summary>
    /// Whether a provider is configured *and* its discovery document has been
    /// resolved. False while discovery has not yet succeeded, so a provider that is
    /// slow or down degrades to "no provider" instead of offering a sign-in that
    /// cannot complete.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// The issuer external identities are recorded under, or null when no provider is
    /// configured. Read from configuration, never from discovery, so a provider that is
    /// momentarily unreachable does not make the app forget which identities it trusts.
    /// </summary>
    string? ConfiguredIssuer { get; }

    /// <summary>
    /// What a browser client needs to run the authorization flow itself, or null when
    /// there is no usable provider. Served to the client so an operator configures the
    /// provider in one place — the container — rather than in the app and its build.
    /// None of it is secret: a public client's id is published by construction.
    /// </summary>
    Task<ExternalProviderInfo?> GetClientInfoAsync(CancellationToken ct = default);
}

/// <summary>The provider coordinates a browser client needs.</summary>
public sealed record ExternalProviderInfo(string Authority, string ClientId, string Scopes);
