using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Server.Arkaine.Meta
{
    public static class MetaApis
    {
        public static void RegisterMetaApis(this WebApplication app)
        {
            app.MapPut("/meta/rating", 
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (Rating rating, IMetaRepository repository) =>
                {
                    await repository.SetRating(rating);
                });
        }
    }
}
