using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// How the app behaves around the OIDC *configuration*, before any sign-in: absent, it
// must be invisible; half-set, it must refuse to start rather than offer a sign-in that
// cannot work; unreachable, it must not stop the app from booting.
public sealed class OidcConfigurationTests
{
    [Fact]
    public async Task No_configuration_leaves_the_feature_invisible()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var config = await (await client.Get("/api/config")).Content.ReadFromJsonAsync<ConfigDto>();

        Assert.False(config!.OidcAvailable);
    }

    [Theory]
    [InlineData("https://id.example.com", null)]
    [InlineData(null, "coffee-tracker")]
    public void Half_configured_provider_prevents_startup(string? authority, string? clientId)
    {
        using var factory = new ApiFactory(oidc: new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = authority,
            ["Oidc:ClientId"] = clientId,
        });

        // The host builds lazily, so the refusal surfaces on the first client creation.
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("must be set together", string.Join(" ", ex.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void An_admin_claim_without_a_value_prevents_startup()
    {
        using var factory = new ApiFactory(oidc: new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = "https://id.example.com",
            ["Oidc:ClientId"] = "coffee-tracker",
            ["Oidc:AdminClaim"] = "groups",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("AdminClaimValue", string.Join(" ", ex.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_provider_does_not_stop_the_app()
    {
        // A provider that resolves to nothing: discovery will fail on every attempt.
        using var factory = new ApiFactory(oidc: new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = "https://oidc.invalid",
            ["Oidc:ClientId"] = "coffee-tracker",
        });
        var client = factory.CreateClient();

        // The app serves requests regardless…
        var config = await (await client.Get("/api/config")).Content.ReadFromJsonAsync<ConfigDto>();

        // …and honestly reports that there is no usable provider, rather than offering a
        // sign-in that would dead-end.
        Assert.False(config!.OidcAvailable);
        Assert.True(config.LocalLoginEnabled);
    }
}
