using System.Net;
using CoffeeTracker.Api;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// Proves the fixed-window rate limiters (per client IP, see Program.cs) actually
// throttle the endpoints they are attached to: the auth endpoints against
// brute-force/credential-stuffing, and the two endpoints whose cost a caller could
// otherwise impose at will. Under the test host every request shares the one "unknown"
// IP partition, so the windows are deterministic.
public sealed class RateLimitTests : IntegrationTest
{
    [Fact]
    public async Task Auth_endpoint_returns_429_after_exceeding_the_window()
    {
        // Unknown-user logins (401) so account lockout never enters the picture — we are
        // isolating the rate limiter. The first 10 are permitted; the 11th is rejected.
        var attempt = new LoginDto("nobody@example.com", "whatever-password");

        for (var i = 0; i < RateLimiterPolicies.AuthPermitsPerMinute; i++)
        {
            var permitted = await Client.Post("/api/auth/login", attempt);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, permitted.StatusCode);
        }

        var rejected = await Client.Post("/api/auth/login", attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task The_anonymous_config_endpoint_returns_429_after_exceeding_its_window()
    {
        // Anonymous, and it reads the database and the provider's discovery document on
        // every call — the one endpoint an unauthenticated caller can reach at will.
        for (var i = 0; i < RateLimiterPolicies.PublicPermitsPerMinute; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Client.Get("/api/config")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Client.Get("/api/config")).StatusCode);
    }

    [Fact]
    public async Task Scanning_returns_429_after_exceeding_its_window()
    {
        var user = await Client.RegisterAsync("scanner@example.com", "Scanner");

        // OCR is off in the test host, so a permitted scan degrades to 503. That is the
        // point: the limiter runs before the handler, so what is counted here is the
        // request, not its outcome.
        for (var i = 0; i < RateLimiterPolicies.ScanPermitsPerMinute; i++)
        {
            var permitted = await Client.PostFile("/api/coffees/scan", ApiClient.RealPng(), "image/png", user.Token);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, permitted.StatusCode);
        }

        var rejected = await Client.PostFile("/api/coffees/scan", ApiClient.RealPng(), "image/png", user.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Each_policy_holds_its_own_budget()
    {
        // Spending the public budget on an unauthenticated poll must not cost a user
        // their ability to sign in — separate policies, separate windows.
        for (var i = 0; i <= RateLimiterPolicies.PublicPermitsPerMinute; i++)
        {
            await Client.Get("/api/config");
        }

        var registered = await Client.RegisterAsync("first@example.com", "First");

        Assert.NotNull(registered.Token);
    }
}
