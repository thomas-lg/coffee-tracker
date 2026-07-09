using System.Security.Cryptography;
using System.Text;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core adapter for <see cref="IRefreshTokenStore"/>. Tokens are 256-bit random
/// values; only their SHA-256 hash is persisted. Rotation and revocation rely on
/// SQLite's single-writer serialisation for atomicity.
/// </summary>
public sealed class EfRefreshTokenStore(
    AppDbContext db,
    TimeProvider clock,
    IOptions<JwtOptions> jwt,
    ILogger<EfRefreshTokenStore> logger) : IRefreshTokenStore
{
    private readonly int _lifetimeDays = jwt.Value.RefreshTokenDays;

    public async Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken ct = default)
    {
        var (raw, hash) = NewToken();
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddDays(_lifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
        });
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(raw, expires);
    }

    public async Task<RefreshRotation> ValidateAndRotateAsync(string presentedToken, CancellationToken ct = default)
    {
        var hash = Hash(presentedToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null)
        {
            return new RefreshRotation(false, null, null);
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // Presenting an already-rotated/revoked token means either a replay or a stolen
        // token being used after the legitimate client already rotated it — revoke the
        // whole family so neither side can continue.
        if (existing.RevokedAtUtc is not null)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking all their sessions.", existing.UserId);
            await RevokeAllAsync(existing.UserId, ct);
            return new RefreshRotation(false, null, null);
        }

        if (existing.ExpiresAtUtc <= now)
        {
            return new RefreshRotation(false, null, null);
        }

        existing.RevokedAtUtc = now;
        var (raw, newHash) = NewToken();
        var expires = now.AddDays(_lifetimeDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
        });
        await db.SaveChangesAsync(ct);

        return new RefreshRotation(true, existing.UserId, new IssuedRefreshToken(raw, expires));
    }

    public async Task RevokeAsync(string presentedToken, CancellationToken ct = default)
    {
        var hash = Hash(presentedToken);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private static (string Raw, string Hash) NewToken()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
