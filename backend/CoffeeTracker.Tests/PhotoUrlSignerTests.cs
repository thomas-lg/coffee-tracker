using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// Photo URLs are the only thing standing between a stored photo and an anonymous reader,
// so the signer's two rejection paths matter as much as its happy path. The integration
// suite covers unsigned and tampered requests; expiry needs a movable clock, which is
// what this file adds.
public sealed class PhotoUrlSignerTests
{
    private const int LifetimeMinutes = 60;

    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private PhotoUrlSigner NewSigner() => new(
        Options.Create(new JwtOptions { Key = new string('k', 64) }),
        Options.Create(new PhotoStorageOptions { SignedUrlLifetimeMinutes = LifetimeMinutes }),
        _clock);

    // Splits "/photos/<name>?exp=..&sig=.." back into the three values Validate takes.
    private static (string FileName, string Exp, string Sig) Parts(string signedUrl)
    {
        var query = signedUrl[(signedUrl.IndexOf('?') + 1)..].Split('&');
        return (
            Uri.UnescapeDataString(signedUrl[("/photos/".Length)..signedUrl.IndexOf('?')]),
            query[0]["exp=".Length..],
            query[1]["sig=".Length..]);
    }

    [Fact]
    public void A_freshly_signed_url_validates()
    {
        var signer = NewSigner();
        var (name, exp, sig) = Parts(signer.Sign("photos/abc.webp")!);

        Assert.True(signer.Validate(name, exp, sig));
    }

    [Fact]
    public void A_signed_url_stops_validating_once_its_lifetime_elapses()
    {
        var signer = NewSigner();
        var (name, exp, sig) = Parts(signer.Sign("photos/abc.webp")!);

        // One second inside the window, then one second past it: the signature is
        // unchanged, so only the clock decides.
        _clock.Now = _clock.Now.AddMinutes(LifetimeMinutes).AddSeconds(-1);
        Assert.True(signer.Validate(name, exp, sig));

        _clock.Now = _clock.Now.AddSeconds(2);
        Assert.False(signer.Validate(name, exp, sig));
    }

    [Fact]
    public void An_expiry_that_is_not_a_number_is_rejected()
    {
        var signer = NewSigner();
        var (name, _, sig) = Parts(signer.Sign("photos/abc.webp")!);

        Assert.False(signer.Validate(name, "not-a-timestamp", sig));
        Assert.False(signer.Validate(name, null, sig));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
