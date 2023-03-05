using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Arkaine.Favourites;
using System.Security.Claims;

namespace Server.Arkaine.B2
{
    public static class B2Apis
    {
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
            ([FromRoute] string file, IB2Service service) => Task.FromResult(service.Preview(file)));

            app.MapGet("/stream/{*file}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async ([FromRoute] string file, CancellationToken cancelationToken, ClaimsPrincipal user, IB2Service service) =>
            {
                string userName = user?.Identity?.Name ?? string.Empty;
                return await service.Stream(userName, file, cancelationToken);
            });
        }
    }
}
