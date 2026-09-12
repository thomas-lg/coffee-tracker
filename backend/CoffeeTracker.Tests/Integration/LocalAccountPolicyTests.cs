using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// The local-account policy over real HTTP: each setting gates its own endpoint, the
// two are independent, and a fresh instance bootstraps its first account without any
// configuration and then closes registration by itself.
public sealed class LocalAccountPolicyTests
{
    [Fact]
    public async Task Sign_in_is_refused_when_local_login_is_disabled()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await client.RegisterAsync("user@example.com", "User");

        factory.SetPolicy(new AccountPolicy(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        var res = await client.Post("/api/auth/login", new LoginDto("user@example.com", ApiClient.DefaultPassword));

        // Distinct from 401: the credentials were never even considered, and the client
        // has to tell the user to use the provider rather than retype their password.
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Registration_is_refused_when_local_registration_is_disabled()
    {
        using var factory = new ApiFactory(registrationEnabled: false);
        var client = factory.CreateClient();

        var res = await client.Post(
            "/api/auth/register",
            new RegisterDto("nope@example.com", ApiClient.DefaultPassword, "Nope"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task The_two_settings_are_independent()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await client.RegisterAsync("admin@example.com", "Admin");

        // Registration open, sign-in closed: an odd combination, but a representable and
        // harmless one — the account is created and simply cannot sign in yet.
        factory.SetPolicy(new AccountPolicy(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        var registered = await client.Post(
            "/api/auth/register",
            new RegisterDto("late@example.com", ApiClient.DefaultPassword, "Late"));
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var login = await client.Post("/api/auth/login", new LoginDto("late@example.com", ApiClient.DefaultPassword));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        factory.SetPolicy(new AccountPolicy(LocalLoginEnabled: true, LocalRegistrationEnabled: true));
        var afterReopen = await client.Post("/api/auth/login", new LoginDto("late@example.com", ApiClient.DefaultPassword));
        Assert.Equal(HttpStatusCode.OK, afterReopen.StatusCode);
    }

    [Fact]
    public async Task A_fresh_instance_registers_its_first_account_then_closes_registration()
    {
        // No policy stamped and nothing configured: exactly a fresh self-hosted
        // install. The operator must be able to create the first account with nothing
        // configured, and the door must shut behind them.
        using var factory = new ApiFactory(stampPolicy: false);
        var client = factory.CreateClient();

        var first = await client.RegisterAsync("owner@example.com", "Owner");
        Assert.True(first.IsAdmin);

        var second = await client.Post(
            "/api/auth/register",
            new RegisterDto("stranger@example.com", ApiClient.DefaultPassword, "Stranger"));
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);

        var config = await (await client.Get("/api/config")).Content.ReadFromJsonAsync<ConfigDto>();
        Assert.False(config!.RegistrationEnabled);
        Assert.True(config.LocalLoginEnabled);
    }

    [Fact]
    public async Task Registration_an_admin_opened_deliberately_stays_open()
    {
        using var factory = new ApiFactory(stampPolicy: false);
        var client = factory.CreateClient();
        await client.RegisterAsync("owner@example.com", "Owner");

        // The admin reopens it. This is not a bootstrap, so the next account must not
        // slam the door again behind them.
        factory.SetPolicy(new AccountPolicy(LocalLoginEnabled: true, LocalRegistrationEnabled: true));

        await client.RegisterAsync("guest1@example.com", "Guest One");
        await client.RegisterAsync("guest2@example.com", "Guest Two");

        var policy = await factory.GetPolicyAsync();
        Assert.True(policy.LocalRegistrationEnabled);
    }

    [Fact]
    public async Task Config_reports_all_three_flags()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        factory.SetPolicy(new AccountPolicy(LocalLoginEnabled: false, LocalRegistrationEnabled: false));

        var config = await (await client.Get("/api/config")).Content.ReadFromJsonAsync<ConfigDto>();

        Assert.False(config!.LocalLoginEnabled);
        Assert.False(config.RegistrationEnabled);
        // No OIDC configured in the test host.
        Assert.False(config.OidcAvailable);
    }
}
