using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Server.Arkaine.B2
{
    public static class B2Apis
    {
        public static void RegisterB2Apis(this WebApplication app)
        {
            app.MapPost("/albums",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (CancellationToken cancelationToken, ClaimsPrincipal user, IB2Service service) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                return Results.Ok(await service.ListAlbums(userName, cancelationToken));
            });

            app.MapPost("/files",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (FilesRequest request, CancellationToken cancelationToken, ClaimsPrincipal user, IB2Service service) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                return Results.Ok(await service.ListFiles(request, userName, cancelationToken));
            });

            app.MapGet("/stream/{bucket}/{*file}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async ([FromRoute] string bucket, [FromRoute] string file, CancellationToken cancelationToken, ClaimsPrincipal user, IB2Service service) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                return await service.Stream(userName, bucket, file, cancelationToken);
            });
        }
    }
}
