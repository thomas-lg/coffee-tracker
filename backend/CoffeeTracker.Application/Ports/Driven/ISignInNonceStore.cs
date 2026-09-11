namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Single-use nonces binding a provider sign-in to a request this instance actually
/// started.
///
/// Without this, any unexpired ID token minted for our client id buys an app session —
/// one leaked from a log, a proxy, or a browser extension included. The client cannot
/// supply this proof itself: a nonce it both generates and presents proves nothing to
/// the server.
/// </summary>
public interface ISignInNonceStore
{
    /// <summary>Issues a nonce for a sign-in about to start.</summary>
    Task<string> IssueAsync(CancellationToken ct = default);

    /// <summary>
    /// Consumes a nonce, returning false when it was never issued, has expired, or has
    /// already been spent. Consuming is atomic so two requests racing on one nonce
    /// cannot both win.
    /// </summary>
    Task<bool> ConsumeAsync(string nonce, CancellationToken ct = default);
}
