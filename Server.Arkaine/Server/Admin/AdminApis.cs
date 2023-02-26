using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Server.Arkaine.Admin
{
    public static class AdminApis
    {
        public static void RegisterAdminApis(this WebApplication app)
        {
            app.MapGet("/settings",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")] 
            (ThumbnailManager manager, IOptions<ArkaineOptions> config) =>
            {
                return Results.Ok(manager.GetSettings());
            });

            app.MapGet("/settings/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (ThumbnailManager manager, ClaimsPrincipal user) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                // Fire and forget this
                Task.Run(() => manager.GenerateThumbnails(userName));
                return Results.Ok();
            });

            app.MapGet("/settings/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (ThumbnailManager manager, IOptions<ArkaineOptions> config) =>
            {
                manager.CancelGeneration();
                return Results.Ok(manager.GetSettings());
            });

            app.MapHub<UpdatesHub>("/updates");
        }
    }
}
