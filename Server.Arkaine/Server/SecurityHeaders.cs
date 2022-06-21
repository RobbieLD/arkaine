using Microsoft.Extensions.Options;
using System.Net;

namespace Server.Arkaine
{
    public class SecurityHeaders
    {
        private readonly RequestDelegate _next;
        public SecurityHeaders(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Response.Headers.Add("Content-Security-Policy", "default-src 'none'; script-src 'self'; img-src 'self'; media-src 'self'; style-src 'self'; connect-src 'self';");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("Cross-Origin-Resource-Policy", "same-origin");
            context.Response.Headers.Add("Cross-Origin-Opener-Policy", "same-origin");
            context.Response.Headers.Add("Cross-Origin-Embedder-Policy", "require-corp");
            context.Response.Headers.Add("Permissions-Policy", "accelerometer=(), ambient-light-sensor=(), autoplay=(), battery=(), camera=(), display-capture=(), document-domain=(), encrypted-media=(), fullscreen=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(), screen-wake-lock=(), speaker=(), sync-xhr=(), usb=(), web-share=(), xr-spatial-tracking=()");

            await _next.Invoke(context);
        }
    }
}
