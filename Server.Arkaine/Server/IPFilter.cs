using Microsoft.Extensions.Options;
using System.Net;

namespace Server.Arkaine
{
    public class IPFilter
    {
        private readonly RequestDelegate _next;
        private readonly IEnumerable<IPAddress> _ipAddresses;
        public IPFilter(RequestDelegate next, IEnumerable<IPAddress> ipAddresses)
        {
            _next = next;
            _ipAddresses = ipAddresses;
        }

        public async Task Invoke(HttpContext context)
        {
            IPAddress ipAddress;

            var forwarded = context.Request.Headers["X-Forwarded-For"];

            if (!string.IsNullOrEmpty(forwarded))
            {
                ipAddress = IPAddress.Parse(forwarded);
            }
            else
            {
                ipAddress = context.Connection.RemoteIpAddress!;
            }
            
            if (!_ipAddresses.Contains(ipAddress))
            {
                Console.WriteLine($"Blocked access from : {ipAddress}");
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            await _next.Invoke(context);
        }
    }
}
