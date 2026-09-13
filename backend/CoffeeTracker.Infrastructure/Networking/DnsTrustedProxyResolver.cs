using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Infrastructure.Networking;

/// <summary>
/// Turns the configured <c>ForwardedHeaders:KnownProxies</c> list into addresses.
///
/// Entries may be addresses or host names. Names matter because a container
/// orchestrator assigns the address, not the operator: pinning the reverse proxy's
/// current IP works until it restarts onto another one, at which point forwarded
/// headers are silently ignored and the rate limiter goes back to seeing every request
/// as the proxy. Naming the proxy — a container name, a service name, a DNS record —
/// is the thing that stays true.
///
/// Resolution happens once, at startup, because that is when the forwarded-headers
/// options are built. A proxy that moves to another address while the app is running
/// therefore stops being trusted until the app restarts — headers are ignored and the
/// rate limiter sees the proxy again, which is the same degraded behaviour as
/// configuring nothing at all. Never the other way round: an address is never trusted
/// because it once belonged to the proxy.
/// </summary>
public sealed class DnsTrustedProxyResolver(ILogger<DnsTrustedProxyResolver> logger)
{
    /// <summary>
    /// Resolves the list. Entries that are neither an address nor a resolvable name are
    /// dropped, never guessed: trusting the wrong hop would let a client forge its own
    /// address. A caller gets fewer trusted proxies than it asked for, never more.
    /// </summary>
    public IReadOnlyList<IPAddress> Resolve(string? configuredValue)
    {
        var resolved = new List<IPAddress>();

        foreach (var entry in (configuredValue ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(entry, out var address))
            {
                resolved.Add(address);
                continue;
            }

            try
            {
                var addresses = Dns.GetHostAddresses(entry);
                if (addresses.Length == 0)
                {
                    logger.LogWarning("Trusted proxy {Proxy} resolved to no address; its forwarded headers are ignored.", entry);
                    continue;
                }

                resolved.AddRange(addresses);
                logger.LogInformation(
                    "Trusted proxy {Proxy} resolved to {Addresses}.",
                    entry,
                    string.Join(", ", addresses.Select(a => a.ToString())));
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                // Dropping the entry costs the real client IP in rate-limit partitioning.
                // Guessing would cost more: an unresolvable name must never become a
                // wildcard, or a client could forge the address it is throttled by.
                logger.LogWarning(
                    ex,
                    "Trusted proxy {Proxy} could not be resolved; its forwarded headers are ignored.",
                    entry);
            }
        }

        return resolved;
    }
}
