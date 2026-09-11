using System.Net;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Turns the configured <c>ForwardedHeaders:KnownProxies</c> list into addresses.
///
/// Entries may be addresses or host names. Names matter because a container
/// orchestrator assigns the address, not the operator: pinning the reverse proxy's
/// current IP works until it restarts onto another one, at which point forwarded
/// headers are silently ignored and the rate limiter goes back to seeing every request
/// as the proxy. Naming the proxy — a container name, a service name, a DNS record —
/// is the thing that stays true.
/// </summary>
public interface ITrustedProxyResolver
{
    /// <summary>
    /// Resolves the list. Entries that are neither an address nor a resolvable name are
    /// dropped, never guessed: trusting the wrong hop would let a client forge its own
    /// address. A caller gets fewer trusted proxies than it asked for, never more.
    /// </summary>
    IReadOnlyList<IPAddress> Resolve(string? configuredValue);
}
