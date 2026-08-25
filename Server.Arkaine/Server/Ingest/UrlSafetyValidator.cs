using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Server.Arkaine.Ingest
{
    public static class UrlSafetyValidator
    {
        public static async Task<bool> IsSafeAsync(string url, CancellationToken cancellationToken)
        {
            if (!TryCreateUri(url, out var uri))
            {
                return false;
            }

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
                return addresses.Length > 0 && addresses.All(IsPublicAddress);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public static async Task<Uri> GetSafeUriAsync(string url, CancellationToken cancellationToken)
        {
            if (!TryCreateUri(url, out var uri))
            {
                throw new InvalidOperationException("Only absolute public HTTPS URLs are supported.");
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            }
            catch (SocketException exception)
            {
                throw new InvalidOperationException("The URL host could not be resolved.", exception);
            }

            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            {
                throw new InvalidOperationException("The URL must resolve only to public IP addresses.");
            }

            return uri;
        }

        public static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var value = BinaryPrimitives.ReadUInt32BigEndian(bytes);

                return !IsInRange(value, 0x00000000, 0xff000000) &&
                       !IsInRange(value, 0x0a000000, 0xff000000) &&
                       !IsInRange(value, 0x64400000, 0xffc00000) &&
                       !IsInRange(value, 0x7f000000, 0xff000000) &&
                       !IsInRange(value, 0xa9fe0000, 0xffff0000) &&
                       !IsInRange(value, 0xac100000, 0xfff00000) &&
                       !IsInRange(value, 0xc0000000, 0xffffff00) &&
                       !IsInRange(value, 0xc0000200, 0xffffff00) &&
                       !IsInRange(value, 0xc0a80000, 0xffff0000) &&
                       !IsInRange(value, 0xc6120000, 0xfffe0000) &&
                       !IsInRange(value, 0xc6336400, 0xffffff00) &&
                       !IsInRange(value, 0xcb007100, 0xffffff00) &&
                       !IsInRange(value, 0xe0000000, 0xf0000000) &&
                       !IsInRange(value, 0xf0000000, 0xf0000000);
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var isLinkLocal = bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80;
                var isUniqueLocal = (bytes[0] & 0xfe) == 0xfc;
                var isSiteLocal = bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0;
                var isMulticast = bytes[0] == 0xff;
                var isDocumentation = bytes[0] == 0x20 &&
                                      bytes[1] == 0x01 &&
                                      bytes[2] == 0x0d &&
                                      bytes[3] == 0xb8;
                var isIpv4Compatible = bytes[..12].All(value => value == 0);

                return !isLinkLocal &&
                       !isUniqueLocal &&
                       !isSiteLocal &&
                       !isMulticast &&
                       !isDocumentation &&
                       (!isIpv4Compatible || IsPublicAddress(new IPAddress(bytes[12..])));
            }

            return false;
        }

        public static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            SocketException? lastException = null;

            foreach (var address in addresses.Where(IsPublicAddress))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                var connected = false;

                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    connected = true;
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException exception)
                {
                    lastException = exception;
                }
                finally
                {
                    if (!connected)
                    {
                        socket.Dispose();
                    }
                }
            }

            throw new HttpRequestException("The outbound connection target is not public or could not be reached.", lastException);
        }

        private static bool TryCreateUri(string url, out Uri uri)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri!))
            {
                return false;
            }

            return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   uri.Port == 443 &&
                   !uri.IsLoopback &&
                   string.IsNullOrEmpty(uri.UserInfo) &&
                   !string.IsNullOrWhiteSpace(uri.DnsSafeHost);
        }

        private static bool IsInRange(uint value, uint network, uint mask)
        {
            return (value & mask) == network;
        }
    }
}
