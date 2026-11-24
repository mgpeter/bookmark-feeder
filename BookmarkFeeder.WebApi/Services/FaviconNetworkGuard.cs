using System.Net;
using System.Net.Sockets;

namespace BookmarkFeeder.WebApi.Services;

/// <summary>
/// Keeps the favicon fetcher on the public internet. Bookmark URLs are user input - any signed-in
/// account, or anyone holding a sync key, can save <c>http://192.168.1.1/</c> or
/// <c>http://webapi/health</c> - and the worker fetches them from inside the deployment's network.
/// <para>
/// The check runs in <see cref="SocketsHttpHandler.ConnectCallback"/>, i.e. on the address actually
/// connected to, after DNS resolution and for every hop. That covers redirects to internal hosts,
/// DNS names that resolve privately (including Aspire/compose service names) and DNS rebinding,
/// none of which a check on the URL string can catch.
/// </para>
/// </summary>
public static class FaviconNetworkGuard
{
    /// <summary>Builds the favicon client's primary handler.</summary>
    /// <param name="allowPrivateNetworks">
    /// Opt-in (<c>Favicon:AllowPrivateNetworks</c>) for self-hosters who bookmark their own LAN
    /// services and accept that any account can then probe that network.
    /// </param>
    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks = false) => new()
    {
        // Real sites redirect http->https and apex->www; each hop goes through ConnectCallback.
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        UseProxy = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = (context, ct) => ConnectAsync(context.DnsEndPoint, allowPrivateNetworks, ct),
    };

    /// <summary>The ConnectCallback: resolves, refuses non-public addresses, then connects.</summary>
    public static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint, bool allowPrivateNetworks, CancellationToken ct)
    {
        var addresses = IPAddress.TryParse(endpoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, ct);

        // Reject the host if ANY answer is private, not just the first: a name that resolves to
        // both a public and an internal address must not get a second chance on retry.
        if (addresses.Length == 0 ||
            (!allowPrivateNetworks && addresses.Any(a => !IsPublic(a))))
        {
            throw new HttpRequestException(
                $"Favicon fetch to {endpoint.Host} blocked: it does not resolve to a public address.");
        }

        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                if (ex is OperationCanceledException) throw;
                last = ex;
            }
        }

        throw new HttpRequestException($"Could not connect to {endpoint.Host}.", last);
    }

    /// <summary>
    /// True only for globally routable unicast addresses. Everything a self-hosted box can reach
    /// but the internet cannot - loopback, RFC 1918, CGNAT (which includes Tailscale's
    /// 100.64.0.0/10), link-local and cloud metadata, ULA - is refused.
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0                                    // 0.0.0.0/8 "this network"
                     || b[0] == 10                                // 10.0.0.0/8
                     || (b[0] == 100 && (b[1] & 0xC0) == 64)      // 100.64.0.0/10 CGNAT, Tailscale
                     || b[0] == 127                               // loopback
                     || (b[0] == 169 && b[1] == 254)              // link-local, cloud metadata
                     || (b[0] == 172 && (b[1] & 0xF0) == 16)      // 172.16.0.0/12
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0)   // 192.0.0.0/24 IETF
                     || (b[0] == 192 && b[1] == 0 && b[2] == 2)   // TEST-NET-1
                     || (b[0] == 192 && b[1] == 168)              // 192.168.0.0/16
                     || (b[0] == 198 && (b[1] & 0xFE) == 18)      // 198.18.0.0/15 benchmarking
                     || (b[0] == 198 && b[1] == 51 && b[2] == 100) // TEST-NET-2
                     || (b[0] == 203 && b[1] == 0 && b[2] == 113) // TEST-NET-3
                     || b[0] >= 224);                             // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            // ::, ::1 and the deprecated IPv4-compatible ::a.b.c.d all start with 96 zero bits.
            if (b.Take(12).All(x => x == 0)) return false;

            // NAT64 (64:ff9b::/96) embeds an IPv4 address; judge the embedded one.
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b &&
                b.Skip(4).Take(8).All(x => x == 0))
            {
                return IsPublic(new IPAddress(b[12..16]));
            }

            return !(address.IsIPv6LinkLocal                      // fe80::/10
                     || address.IsIPv6SiteLocal                   // fec0::/10 (deprecated)
                     || address.IsIPv6Multicast                   // ff00::/8
                     || (b[0] & 0xFE) == 0xFC                     // fc00::/7 unique local
                     || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8)); // docs
        }

        return false;
    }
}
