using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Spent tokens held in memory until they expire on their own.
///
/// Memory rather than the database on purpose: an entry is worthless once the token it
/// refers to has expired, which is minutes away, and the only cost of losing the set on
/// restart is a window in which a token could be replayed — narrower than the restart
/// itself. Persisting them would buy little and leave rows to sweep.
/// </summary>
public sealed class MemoryCacheUsedTokenRegistry(IMemoryCache cache, TimeProvider timeProvider) : IUsedTokenRegistry
{
    private const string Prefix = "spent-id-token:";

    public Task<bool> TryConsumeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var remaining = expiresAt - timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            // Already expired: lifetime validation has refused it anyway, and caching it
            // would only grow the set.
            return Task.FromResult(false);
        }

        var key = Prefix + tokenId;

        // TryGetValue then Set is not atomic on its own, so serialise it: two requests
        // racing with the same token must not both be told yes.
        lock (this)
        {
            if (cache.TryGetValue(key, out _))
            {
                return Task.FromResult(false);
            }

            cache.Set(key, true, remaining);
            return Task.FromResult(true);
        }
    }
}
