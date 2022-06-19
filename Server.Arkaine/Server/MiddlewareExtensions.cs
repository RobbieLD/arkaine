﻿using System.Net;

namespace Server.Arkaine
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseIPFilter(this IApplicationBuilder builder, IPAddress ipAddress)
        {
            return builder.UseMiddleware<IPFilter>(ipAddress);
        }
    }
}
