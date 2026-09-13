using CoffeeTracker.Infrastructure.Identity;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CoffeeTracker.Tests;

// The spent-ID-token set is what stops a provider token being replayed: sign in once
// with a stolen id_token and it works, twice and it must not. Everything here is about
// the second attempt failing.
public sealed class MemoryCacheUsedTokenRegistryTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private MemoryCacheUsedTokenRegistry NewRegistry() => new(_cache, _clock);

    private DateTimeOffset InFiveMinutes => _clock.GetUtcNow().AddMinutes(5);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task The_first_use_of_a_token_is_allowed()
    {
        Assert.True(await NewRegistry().TryConsumeAsync("jti-1", InFiveMinutes));
    }

    [Fact]
    public async Task Presenting_the_same_token_twice_is_refused()
    {
        var registry = NewRegistry();
        var expiry = InFiveMinutes;

        Assert.True(await registry.TryConsumeAsync("jti-1", expiry));
        Assert.False(await registry.TryConsumeAsync("jti-1", expiry));
    }

    [Fact]
    public async Task Spending_one_token_does_not_spend_another()
    {
        var registry = NewRegistry();

        Assert.True(await registry.TryConsumeAsync("jti-1", InFiveMinutes));
        Assert.True(await registry.TryConsumeAsync("jti-2", InFiveMinutes));
    }

    [Fact]
    public async Task An_already_expired_token_is_refused_without_being_remembered()
    {
        var registry = NewRegistry();
        var expired = _clock.GetUtcNow().AddMinutes(-1);

        Assert.False(await registry.TryConsumeAsync("jti-1", expired));

        // Refused, and not cached: lifetime validation has already rejected it, so
        // holding the entry would grow the set for nothing.
        Assert.False(_cache.TryGetValue("spent-id-token:jti-1", out _));
    }

    [Fact]
    public async Task A_token_expiring_exactly_now_is_refused()
    {
        Assert.False(await NewRegistry().TryConsumeAsync("jti-1", _clock.GetUtcNow()));
    }

    // The reason TryConsumeAsync holds a lock at all. TryGetValue-then-Set is two
    // operations, so without it two requests arriving together with the same stolen
    // token can both read "not spent" and both be let in — which is precisely the
    // replay the registry exists to stop. Removing the lock makes this fail.
    [Fact]
    public async Task Only_one_of_many_simultaneous_uses_of_one_token_is_allowed()
    {
        var registry = NewRegistry();
        var expiry = InFiveMinutes;
        using var start = new Barrier(32);

        var attempts = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            // Line every caller up first, so they contend rather than run in sequence.
            start.SignalAndWait();
            return registry.TryConsumeAsync("jti-contended", expiry);
        }));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(allowed => allowed));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
