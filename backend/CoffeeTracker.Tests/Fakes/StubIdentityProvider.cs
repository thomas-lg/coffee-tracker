using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Tests.Fakes;

/// <summary>
/// An identity provider a test can describe in one line: an issuer, or none.
/// </summary>
public sealed class StubIdentityProvider(string? issuer) : IExternalIdentityProvider
{
    public string? ConfiguredIssuer => issuer;

    public Task<OidcClientConfigDto?> GetClientInfoAsync(CancellationToken ct = default) =>
        Task.FromResult(issuer is null ? null : new OidcClientConfigDto(issuer, "test-client", "openid profile email", "Test Provider"));
}
