using System.Net;

namespace Server.Arkaine
{
    public class IPFilter
    {
        private readonly RequestDelegate _next;
        private readonly IEnumerable<IPAddress> _ipAddresses;
        private readonly ILogger<IPFilter> _logger;

        public IPFilter(RequestDelegate next, IEnumerable<IPAddress> ipAddresses, ILogger<IPFilter> logger)
        {
            _next = next;
            _ipAddresses = ipAddresses;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress;

            if (ipAddress == null || !_ipAddresses.Any(allowedAddress => AreEqual(allowedAddress, ipAddress)))
            {
                _logger.LogWarning("Blocked access from {RemoteIpAddress}", ipAddress);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            await _next.Invoke(context);
        }

        private static bool AreEqual(IPAddress first, IPAddress second)
        {
            if (first.IsIPv4MappedToIPv6)
            {
                first = first.MapToIPv4();
            }

            if (second.IsIPv4MappedToIPv6)
            {
                second = second.MapToIPv4();
            }

            return first.Equals(second);
        }
    }
}
