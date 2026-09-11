namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Remembers provider tokens that have already been exchanged for a session, so each
/// one buys at most one.
///
/// Without this, a token captured anywhere it passes — a log, a proxy, a browser
/// extension — can be exchanged again for as long as it remains valid. The OIDC nonce
/// would bind a token to one authorization request, but the client library owns nonce
/// generation and offers no way to impose one from the server; a nonce the client both
/// creates and presents proves nothing. Single use is what is actually enforceable
/// here, and it closes replay.
/// </summary>
public interface IUsedTokenRegistry
{
    /// <summary>
    /// Marks a token as spent, returning false when it already was. Atomic, so two
    /// requests racing with the same token cannot both win. Entries are kept only until
    /// <paramref name="expiresAt"/>: past it the token is refused on its own merits.
    /// </summary>
    Task<bool> TryConsumeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken ct = default);
}
