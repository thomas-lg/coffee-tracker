using System.Net;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// The headers have to reach real responses, including the anonymous ones a browser
// meets before it has a session, which is exactly when the app is least protected.
public sealed class SecurityHeadersEndToEndTests : IntegrationTest
{
    [Theory]
    [InlineData("/api/config")]   // anonymous, reached before sign-in
    [InlineData("/health")]       // anonymous, outside the auth fallback policy
    [InlineData("/api/coffees")]  // authenticated: answers 401, still a response
    public async Task Every_response_carries_the_security_headers(string path)
    {
        var response = await Client.GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains(
            "frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_photo_request_is_covered_too()
    {
        // Unsigned /photos requests are refused by the signing middleware, which
        // short-circuits before the static-file middleware, a path that would miss
        // the headers if they were applied any later in the pipeline.
        var response = await Client.GetAsync("/photos/nothing.jpg");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }
}
