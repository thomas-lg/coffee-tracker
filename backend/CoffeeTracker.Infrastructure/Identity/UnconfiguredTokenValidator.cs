using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Stands in when no OIDC provider is configured: there is no issuer to trust, so no
/// token can be believed. Registered instead of the real validator so nothing has to
/// construct a provider adapter that has nothing to point at.
/// </summary>
public sealed class UnconfiguredTokenValidator : IExternalTokenValidator
{
    public Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken ct = default) =>
        Task.FromResult<ExternalIdentity?>(null);
}
