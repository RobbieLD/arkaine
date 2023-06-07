using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Server.Arkaine.Tags
{
    public static class TagApis
    {
        public static void RegisterTagApis(this WebApplication app)
        {
            app.MapGet("/tags/{*file}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async ([FromRoute] string fileName, ITagService service) =>
                {
                    return Results.Ok(await service.GetTagsForFile(fileName));
                });

            app.MapPost("/tags/add",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (AddTagRequest request, ITagService service) =>
                {
                    await service.AddTag(request);
                    return Results.Ok();
                });
        }
    }
}
