using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Stands in when no OIDC provider is configured. Registered instead of the real
/// adapter so every caller can ask the same question and get a truthful "no",
/// rather than reading configuration for itself.
/// </summary>
public sealed class UnconfiguredIdentityProvider : IExternalIdentityProvider
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(false);

    public string? ConfiguredIssuer => null;
}
