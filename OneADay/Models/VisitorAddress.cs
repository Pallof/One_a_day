using System.Net;
using System.Net.Sockets;

namespace OneADay.Models;

/// <summary>
/// The part of a visitor's address that per-address limits count by.
/// </summary>
/// <remarks>
/// An IPv4 address is one household or office. An IPv6 home connection gets a whole block of
/// about 18 billion billion addresses (a /64), and its devices pick new ones from it at will, so
/// counting whole IPv6 addresses would let one script walk past any per-address limit. The
/// block itself, the first half of the address, is what one household controls.
/// </remarks>
public static class VisitorAddress
{
    /// <summary>
    /// The address as text, or for IPv6 its /64 block. Null when there is no address, which
    /// callers read as "no per-address limit for this one".
    /// </summary>
    public static string? Key(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        // An IPv4 visitor can arrive written as IPv6 (::ffff:203.0.113.7); it's the same visitor.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);   // keep the /64, drop the part each device chooses
        return $"{new IPAddress(bytes)}/64";
    }
}
