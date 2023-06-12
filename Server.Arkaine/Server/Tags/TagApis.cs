using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Server.Arkaine.Tags
{
    public static class TagApis
    {
        public static void RegisterTagApis(this WebApplication app)
        {
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
