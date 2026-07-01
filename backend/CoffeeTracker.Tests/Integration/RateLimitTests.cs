using System.Net;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// Proves the fixed-window auth rate limiter (10 req/min per client IP, see Program.cs)
// actually throttles the auth endpoints — the primary brute-force/credential-stuffing
// defense. Under the test host every request shares the one "unknown" IP partition, so
// the window is deterministic.
public sealed class RateLimitTests : IntegrationTest
{
    [Fact]
    public async Task Auth_endpoint_returns_429_after_exceeding_the_window()
    {
        // Unknown-user logins (401) so account lockout never enters the picture — we are
        // isolating the rate limiter. The first 10 are permitted; the 11th is rejected.
        var attempt = new LoginDto("nobody@example.com", "whatever-password");

        for (var i = 0; i < 10; i++)
        {
            var permitted = await Client.Post("/api/auth/login", attempt);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, permitted.StatusCode);
        }

        var rejected = await Client.Post("/api/auth/login", attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }
}
