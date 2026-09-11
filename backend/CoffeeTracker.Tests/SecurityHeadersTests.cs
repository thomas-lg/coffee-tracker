using CoffeeTracker.Api;
using Xunit;

namespace CoffeeTracker.Tests;

// The content-security-policy is assembled rather than written out, so the properties
// that make it worth having are asserted here — a header read off the Development host
// the integration tests boot would show the relaxed policy and prove nothing.
public class SecurityHeadersTests
{
    private static string Directive(string policy, string name) =>
        policy.Split("; ").Single(d => d.StartsWith(name + " ", StringComparison.Ordinal));

    [Fact]
    public void Outside_development_no_inline_script_is_allowed()
    {
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: false, oidcAuthority: null);

        Assert.Equal("script-src 'self'", Directive(policy, "script-src"));
        Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_widens_script_src_for_the_swagger_ui()
    {
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: true, oidcAuthority: null);

        Assert.Contains("'unsafe-inline'", Directive(policy, "script-src"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_app_may_not_be_framed_and_leaks_no_referrer_target()
    {
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: false, oidcAuthority: null);

        Assert.Equal("frame-ancestors 'none'", Directive(policy, "frame-ancestors"));
        Assert.Equal("object-src 'none'", Directive(policy, "object-src"));
        Assert.Equal("base-uri 'self'", Directive(policy, "base-uri"));
    }

    [Fact]
    public void With_no_provider_the_browser_may_only_call_the_app_itself()
    {
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: false, oidcAuthority: null);

        Assert.Equal("connect-src 'self'", Directive(policy, "connect-src"));
    }

    [Theory]
    // Only the origin is granted: the configured path is not part of what the browser
    // may reach, and a provider on a non-default port keeps it.
    [InlineData("https://id.example.com/", "https://id.example.com")]
    [InlineData("https://id.example.com/realms/coffee", "https://id.example.com")]
    [InlineData("https://id.example.com:8443", "https://id.example.com:8443")]
    public void A_configured_provider_origin_is_reachable(string authority, string expected)
    {
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: false, authority);

        Assert.Equal($"connect-src 'self' {expected}", Directive(policy, "connect-src"));
    }

    [Fact]
    public void A_provider_that_is_not_an_absolute_url_is_ignored_rather_than_thrown_on()
    {
        // Options validation refuses this at startup; the policy is built before that
        // failure surfaces, so it must not be the thing that throws.
        var policy = SecurityHeaders.BuildContentSecurityPolicy(isDevelopment: false, "not-a-url");

        Assert.Equal("connect-src 'self'", Directive(policy, "connect-src"));
    }
}
