using System.Net;

namespace Server.Arkaine
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseIPFilter(this IApplicationBuilder builder, IEnumerable<IPAddress> ipAddresses)
        {
            return builder.UseMiddleware<IPFilter>(ipAddresses);
        }

        public static IApplicationBuilder UserSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeaders>();
        }
    }
}
