using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace ProPresenterObsBridge.Util;

public sealed class HostResolver(ILogger<HostResolver> logger)
{
    /// <summary>
    /// Resolves a hostname to an IP address, preferring IPv4 then IPv6.
    /// If the host is already an IP address, returns it directly.
    /// </summary>
    public async Task<IPAddress?> ResolveAsync(string host, CancellationToken ct = default)
    {
        if (IPAddress.TryParse(host, out var directIp))
            return directIp;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0)
            {
                logger.LogWarning("DNS resolution for '{Host}' returned no addresses", host);
                return null;
            }

            // Prefer IPv4, fall back to IPv6
            var ipv4 = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork);
            var ipv6 = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetworkV6);
            var chosen = ipv4 ?? ipv6 ?? addresses[0];
            logger.LogDebug("Resolved '{Host}' to {Address}", host, chosen);
            return chosen;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to resolve '{Host}' - {Message}", host, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Formats an IP address for use in a URI, wrapping IPv6 addresses in brackets.
    /// </summary>
    public static string FormatForUri(IPAddress address)
    {
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();
    }
}
