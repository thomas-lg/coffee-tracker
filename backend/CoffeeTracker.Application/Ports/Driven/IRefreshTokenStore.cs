namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Persists rotating refresh tokens (only their hashes) so access tokens can stay
/// short-lived while sessions remain long-lived and individually revocable.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Issues a new opaque refresh token for the user, storing only its hash.</summary>
    Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented token and, if valid, atomically rotates it (revokes the old
    /// one and issues a replacement). Reuse of an already-rotated token is treated as
    /// possible theft and revokes the user's whole token family.
    /// </summary>
    Task<RefreshRotation> ValidateAndRotateAsync(string presentedToken, CancellationToken ct = default);

    /// <summary>Revokes a single presented token (per-session logout). No-op if unknown.</summary>
    Task RevokeAsync(string presentedToken, CancellationToken ct = default);

    /// <summary>Revokes every outstanding token for a user (password change, global sign-out).</summary>
    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshRotation(bool Succeeded, string? UserId, IssuedRefreshToken? Replacement);
