using System.Net;
using System.Net.Sockets;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Infrastructure.Networking;

/// <summary>
/// Resolves the trusted-proxy list through DNS.
///
/// Resolution happens once, at startup, because that is when the forwarded-headers
/// options are built. A proxy that moves to another address while the app is running
/// therefore stops being trusted until the app restarts — headers are ignored and the
/// rate limiter sees the proxy again, which is the same degraded behaviour as
/// configuring nothing at all. Never the other way round: an address is never trusted
/// because it once belonged to the proxy.
/// </summary>
public sealed class DnsTrustedProxyResolver(ILogger<DnsTrustedProxyResolver> logger) : ITrustedProxyResolver
{
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
