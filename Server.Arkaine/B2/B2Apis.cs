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
                return await service.GetToken(
                    builder.Configuration["B2_KEY"],
                    builder.Configuration["B2_KEY_ID"],
                    builder.Configuration["B2:AuthUrl"]);
            });
        }
    }
}
