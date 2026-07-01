using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// GET /api/config is the anonymous bootstrap endpoint the SPA and the CI e2e
// readiness probe depend on. Guards both the AllowAnonymous contract and that it
// reports the registration gate accurately.
public sealed class ConfigEndpointTests : IntegrationTest
{
    [Fact]
    public async Task Config_is_anonymous_and_reports_registration_enabled()
    {
        // No Authorization header — must still succeed under the global auth fallback.
        var res = await Client.Get("/api/config");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var config = await res.Content.ReadFromJsonAsync<ConfigDto>();
        Assert.NotNull(config);
        Assert.True(config!.RegistrationEnabled); // default factory has registration on
    }

    [Fact]
    public async Task Config_reports_registration_disabled_when_gate_is_off()
    {
        using var closed = new ApiFactory(registrationEnabled: false);
        var client = closed.CreateClient();

        var res = await client.Get("/api/config");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var config = await res.Content.ReadFromJsonAsync<ConfigDto>();
        Assert.NotNull(config);
        Assert.False(config!.RegistrationEnabled);
    }
}
