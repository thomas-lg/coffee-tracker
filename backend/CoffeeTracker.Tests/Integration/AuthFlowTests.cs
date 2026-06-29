using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// End-to-end auth flow over real HTTP: the first-user-is-admin bootstrap, the
// REGISTRATION_ENABLED gate, login success/failure, and the duplicate/weak-input
// rejections. Each test gets its own fresh DB (see IntegrationTest).
public sealed class AuthFlowTests : IntegrationTest
{
    [Fact]
    public async Task First_user_becomes_admin_and_subsequent_users_do_not()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        var second = await Client.RegisterAsync("second@example.com", "Second");

        Assert.True(admin.IsAdmin);
        Assert.False(second.IsAdmin);
        Assert.False(string.IsNullOrWhiteSpace(admin.Token));
        Assert.NotEqual(admin.UserId, second.UserId);
    }

    [Fact]
    public async Task Login_returns_a_token_for_valid_credentials()
    {
        var registered = await Client.RegisterAsync("login@example.com", "Login User");

        var res = await Client.Post("/api/auth/login", new LoginDto("login@example.com", ApiClient.DefaultPassword));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal(registered.UserId, auth.UserId);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        await Client.RegisterAsync("wrongpw@example.com", "Wrong PW");

        var res = await Client.Post("/api/auth/login", new LoginDto("wrongpw@example.com", "not-the-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_for_unknown_user_is_unauthorized()
    {
        var res = await Client.Post("/api/auth/login", new LoginDto("nobody@example.com", ApiClient.DefaultPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_registration_conflicts()
    {
        await Client.RegisterAsync("dupe@example.com", "First");

        var res = await Client.Post("/api/auth/register", new RegisterDto("dupe@example.com", ApiClient.DefaultPassword, "Second"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Registration_below_minimum_password_length_is_rejected()
    {
        // RegisterDto requires an 8+ char password; model validation rejects it
        // (400) before the request ever reaches the auth service.
        var res = await Client.Post("/api/auth/register", new RegisterDto("short@example.com", "tiny", "Shorty"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Registration_is_refused_when_disabled()
    {
        // A dedicated app instance with the gate off — the default Client (gate on)
        // is never booted thanks to IntegrationTest's lazy client.
        using var closed = new ApiFactory(registrationEnabled: false);
        var client = closed.CreateClient();

        var res = await client.Post("/api/auth/register", new RegisterDto("late@example.com", ApiClient.DefaultPassword, "Too Late"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
