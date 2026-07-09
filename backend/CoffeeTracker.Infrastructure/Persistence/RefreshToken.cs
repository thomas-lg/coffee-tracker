namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// A persisted refresh token. Only the SHA-256 hash of the opaque token is stored,
/// never the token itself, so a database leak can't be replayed to mint sessions.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>The owning user (FK to AspNetUsers).</summary>
    public string UserId { get; set; } = "";

    /// <summary>Uppercase hex SHA-256 of the opaque token presented by the client.</summary>
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set when the token is rotated or revoked; null while active.</summary>
    public DateTime? RevokedAtUtc { get; set; }
}
