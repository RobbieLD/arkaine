using Microsoft.Extensions.Options;
using System.Net;

namespace Server.Arkaine
{
    public class IPFilter
    {
        private readonly RequestDelegate _next;
        private readonly IPAddress _ipAddress;
        public IPFilter(RequestDelegate next, IPAddress ipAddress)
        {
            _next = next;
            _ipAddress = ipAddress;
        }

        public async Task Invoke(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress;

            if (!ipAddress?.Equals(_ipAddress) ?? false)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
            await _next.Invoke(context);
        }
    }
}
