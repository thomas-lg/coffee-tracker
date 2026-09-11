using System.Net;
using CoffeeTracker.Infrastructure.Networking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoffeeTracker.Tests;

// What the app decides to trust for forwarded headers. Getting this wrong in the
// permissive direction lets a client forge the address it is rate-limited by, so every
// case here is about the list coming back with fewer entries than asked for, never more.
public sealed class DnsTrustedProxyResolverTests
{
    private static readonly DnsTrustedProxyResolver Resolver = new(NullLogger<DnsTrustedProxyResolver>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",  ,")]
    public void NothingConfigured_TrustsNothing(string? configured) =>
        Assert.Empty(Resolver.Resolve(configured));

    [Fact]
    public void AnAddressIsTakenAsWritten()
    {
        var resolved = Resolver.Resolve("172.19.0.39");

        Assert.Equal([IPAddress.Parse("172.19.0.39")], resolved);
    }

    [Fact]
    public void AListIsResolvedEntryByEntry()
    {
        var resolved = Resolver.Resolve("172.19.0.39, 10.0.0.1 ,::1");

        Assert.Equal(
            [IPAddress.Parse("172.19.0.39"), IPAddress.Parse("10.0.0.1"), IPAddress.Parse("::1")],
            resolved);
    }

    [Fact]
    public void AHostNameIsResolved()
    {
        // localhost is the one name every machine running these tests can resolve.
        var resolved = Resolver.Resolve("localhost");

        Assert.NotEmpty(resolved);
        Assert.All(resolved, a => Assert.True(IPAddress.IsLoopback(a)));
    }

    [Fact]
    public void AnUnresolvableNameIsDropped_NotGuessed()
    {
        // .invalid is reserved by RFC 2606 precisely so it can never resolve.
        var resolved = Resolver.Resolve("no-such-proxy.invalid");

        // Dropping it costs the real client IP in rate-limit partitioning. Turning it
        // into a wildcard would cost the partitioning itself.
        Assert.Empty(resolved);
    }

    [Fact]
    public void OneBadEntryDoesNotDiscardTheGoodOnes()
    {
        var resolved = Resolver.Resolve("no-such-proxy.invalid, 172.19.0.39");

        Assert.Equal([IPAddress.Parse("172.19.0.39")], resolved);
    }
}
