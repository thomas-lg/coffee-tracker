using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// The admin settings endpoint over real HTTP: the policy gates it, and the lock-out
// guard surfaces as a 409 the client can explain rather than a silent no-op.
public sealed class AdminSettingsAuthorizationTests : IntegrationTest
{
    [Fact]
    public async Task Settings_endpoints_enforce_the_admin_policy()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        var user = await Client.RegisterAsync("user@example.com", "User");

        Assert.True(admin.IsAdmin);
        Assert.False(user.IsAdmin);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Get("/api/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.Get("/api/admin/settings", user.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.Get("/api/admin/settings", admin.Token)).StatusCode);

        var body = new AccountSettingsDto(LocalLoginEnabled: true, LocalRegistrationEnabled: false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Put("/api/admin/settings", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.Put("/api/admin/settings", body, user.Token)).StatusCode);
    }

    [Fact]
    public async Task An_admin_can_read_and_change_the_policy()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        var res = await Client.Put(
            "/api/admin/settings",
            new AccountSettingsDto(LocalLoginEnabled: true, LocalRegistrationEnabled: false),
            admin.Token);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var stored = await (await Client.Get("/api/admin/settings", admin.Token))
            .Content.ReadFromJsonAsync<AccountSettingsDto>();
        Assert.True(stored!.LocalLoginEnabled);
        Assert.False(stored.LocalRegistrationEnabled);

        // And the anonymous bootstrap config agrees with what the admin sees.
        var config = await (await Client.Get("/api/config")).Content.ReadFromJsonAsync<ConfigDto>();
        Assert.False(config!.RegistrationEnabled);
    }

    [Fact]
    public async Task Disabling_sign_in_is_refused_with_an_explanation()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        // No provider is configured in the test host, so this would be the last door.
        var res = await Client.Put(
            "/api/admin/settings",
            new AccountSettingsDto(LocalLoginEnabled: false, LocalRegistrationEnabled: true),
            admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetailsBody>();
        Assert.Contains("identity provider", problem!.Detail, StringComparison.OrdinalIgnoreCase);

        // And the refusal really left the setting alone.
        var stored = await (await Client.Get("/api/admin/settings", admin.Token))
            .Content.ReadFromJsonAsync<AccountSettingsDto>();
        Assert.True(stored!.LocalLoginEnabled);
    }

    private sealed record ProblemDetailsBody(string? Detail);
}
