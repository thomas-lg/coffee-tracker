using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// End-to-end refresh-token lifecycle over real HTTP: rotation on refresh, the
// reuse-detection that revokes the whole token family (stolen-token defence), and
// logout revocation. Each test stays well under the 10/min auth rate limit.
public sealed class RefreshTokenFlowTests : IntegrationTest
{
    private async Task<AuthResponseDto> RefreshedAsync(string refreshToken)
    {
        var res = await Client.Post("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    [Fact]
    public async Task Registration_and_login_issue_a_refresh_token_pair()
    {
        var registered = await Client.RegisterAsync("refresh@example.com", "Refresh");
        Assert.False(string.IsNullOrWhiteSpace(registered.RefreshToken));
        Assert.True(registered.RefreshExpiresAt > registered.ExpiresAt); // session outlives the access token

        var loginRes = await Client.Post("/api/auth/login", new LoginDto("refresh@example.com", ApiClient.DefaultPassword));
        var login = (await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.NotEqual(registered.RefreshToken, login.RefreshToken); // each login is its own session
    }

    [Fact]
    public async Task Refresh_rotates_the_pair_and_the_new_access_token_works()
    {
        var auth = await Client.RegisterAsync("rotate@example.com", "Rotate");

        var refreshed = await RefreshedAsync(auth.RefreshToken);

        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        Assert.NotEqual(auth.RefreshToken, refreshed.RefreshToken); // rotated, not re-issued
        Assert.Equal(auth.UserId, refreshed.UserId);

        // The freshly issued access token is accepted by a protected endpoint.
        var api = await Client.Get("/api/coffees", refreshed.Token);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_fails_and_revokes_the_family()
    {
        var auth = await Client.RegisterAsync("reuse@example.com", "Reuse");
        var refreshed = await RefreshedAsync(auth.RefreshToken);

        // Presenting the OLD (already-rotated) token is treated as theft → 401 ...
        var reuse = await Client.Post("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ... and the whole family is revoked: the CURRENT token no longer works either.
        var family = await Client.Post("/api/auth/refresh", new { refreshToken = refreshed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, family.StatusCode);
    }

    [Fact]
    public async Task Refresh_after_logout_is_rejected()
    {
        var auth = await Client.RegisterAsync("logout@example.com", "Logout");

        var logout = await Client.Post("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var res = await Client.Post("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_is_rejected()
    {
        var res = await Client.Post("/api/auth/refresh", new { refreshToken = "totally-made-up-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
