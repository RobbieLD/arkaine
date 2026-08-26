using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Server.Arkaine.Favourites;
using System.Security.Claims;

namespace Server.Arkaine.B2
{
    public static class B2Apis
    {
        // Thumbnails are generated once and never rewritten in place, so they can be held
        // for a long time. Originals live in B2 and can be replaced at the same path, so
        // they get a much shorter window. Both are `private`: the content is behind cookie
        // auth and must never be stored by a shared proxy.
        private static readonly CacheControlHeaderValue PreviewCacheControl = new()
        {
            Private = true,
            MaxAge = TimeSpan.FromDays(7)
        };

        private static readonly CacheControlHeaderValue StreamCacheControl = new()
        {
            Private = true,
            MaxAge = TimeSpan.FromHours(1)
        };

        public static void RegisterB2Apis(this WebApplication app)
        {
            app.MapPost("/files",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (FilesRequest request, CancellationToken cancelationToken, ClaimsPrincipal user, IB2Service service, IFavouritesService favouritesService) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                return Results.Ok(await service.ListFiles(request, userName, favouritesService, cancelationToken));
            });

            app.MapGet("/preview/{*file}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            ([FromRoute] string file, HttpContext context, IB2Service service) =>
            {
                var result = service.Preview(file);

                // Only cache a real thumbnail - a 404 here just means it has not been
                // generated yet, and caching that would hide it once it appears.
                if (result is IFileHttpResult)
                {
                    context.Response.GetTypedHeaders().CacheControl = PreviewCacheControl;
                }

                return result;
            });

            app.MapGet("/stream/{*file}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async ([FromRoute] string file, CancellationToken cancelationToken, ClaimsPrincipal user, HttpContext context, IB2Service service) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                var result = await service.Stream(userName, file, cancelationToken);
                context.Response.GetTypedHeaders().CacheControl = StreamCacheControl;
                return result;
            });
        }
    }
}
