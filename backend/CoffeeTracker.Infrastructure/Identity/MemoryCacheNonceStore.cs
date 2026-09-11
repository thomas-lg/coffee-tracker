using System.Security.Cryptography;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Sign-in nonces held in memory for the few minutes a sign-in takes.
///
/// Memory rather than the database on purpose: a nonce is worthless after one use and
/// after a few minutes, and the only cost of losing the set on restart is that a
/// sign-in in flight has to be started again. Persisting them would buy nothing and
/// leave rows to expire.
/// </summary>
public sealed class MemoryCacheNonceStore(IMemoryCache cache) : ISignInNonceStore
{
    /// <summary>
    /// Long enough for a person to authenticate at the provider, including a password
    /// manager and a second factor; short enough that a leaked nonce is stale.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string Prefix = "signin-nonce:";

    public Task<string> IssueAsync(CancellationToken ct = default)
    {
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        cache.Set(Prefix + nonce, true, Lifetime);
        return Task.FromResult(nonce);
    }

    public Task<bool> ConsumeAsync(string nonce, CancellationToken ct = default)
    {
        var key = Prefix + nonce;

        // TryGetValue then Remove is not atomic on its own, so take a lock keyed on the
        // store: two requests racing with the same nonce must not both be told yes.
        lock (this)
        {
            if (!cache.TryGetValue(key, out _))
            {
                return Task.FromResult(false);
            }

            cache.Remove(key);
            return Task.FromResult(true);
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
