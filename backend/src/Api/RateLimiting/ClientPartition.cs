using System.Net;
using System.Net.Sockets;

namespace DjVisualizer.Api.RateLimiting;

/// <summary>
/// The rate-limit partition for a caller. IPv4 addresses are used as they are; IPv6 addresses are
/// grouped by their /64, because one subscriber is routinely handed a whole /64 and could
/// otherwise rotate through addresses to get a fresh allowance on every request.
/// </summary>
public static class ClientPartition
{
    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
