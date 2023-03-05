using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Server.Arkaine.Favourites
{
    public static class FavouriteApis
    {
        public static void RegisterFavouritesApis(this WebApplication app)
        {
            app.MapPut("/favourite",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (FavouriteRequest request, ClaimsPrincipal user, IFavouritesService service) =>
                {
                    string userName = user?.Identity?.Name ?? string.Empty;

                    if (string.IsNullOrEmpty(userName))
                    {
                        return Results.BadRequest("User name must be applied");
                    }

                    await service.AddFavourite(request.FileName, userName);
                    return Results.Ok();
                });

            app.MapGet("/favourites",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (ClaimsPrincipal user, IFavouritesService service) =>
                {
                    string userName = user?.Identity?.Name ?? string.Empty;

                    if (string.IsNullOrEmpty(userName))
                    {
                        return Results.BadRequest("User name must be aupplied");
                    }

                    return Results.Ok(await service.GetAllFavourites(userName));
                });
        }
    }
}
