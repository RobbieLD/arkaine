using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Server.Arkaine.B2
{
    public static class B2Apis
    {
        public static void RegisterB2Apis(this WebApplication app, WebApplicationBuilder builder)
        {
            app.MapGet("/token",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async ([FromServices] IB2Service service) =>
            {
                try
                {
                    return Results.Text(
                        await service.GetToken(
                            builder.Configuration["B2_KEY"],
                            builder.Configuration["B2_KEY_ID"],
                            builder.Configuration["B2:AuthUrl"]),
                        "application/json; charset=utf-8");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapPost("/albums",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (AlbumsRequest request, IB2Service service) =>
            {
                try
                {
                    return Results.Text(await service.ListAlbums(request), "application/json; charset=utf-8");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapPost("/files",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (FilesRequest request, IB2Service service) =>
            {
                try
                {
                    return Results.Text(await service.ListFiles(request), "application/json; charset=utf-8");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });
        }
    }
}
