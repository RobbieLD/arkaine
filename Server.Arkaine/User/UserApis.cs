using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using System.Security.Claims;

namespace Server.Arkaine.User
{
    public static class UserApis
    {
        public static void RegisterUserApis(this WebApplication app)
        {
            app.MapPost("/login",
                [AllowAnonymous]
            async (LoginRequest request, HttpContext context, CancellationToken cancellationToken, IOptions<ArkaineOptions> config, IUserService userService, IB2Service tokenService, IMemoryCache cache) =>
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return Results.BadRequest("Username and password are required");
                }
                
                var roles = await userService.LoginUserAsync(request.Username, request.Password);
                if (roles == null)
                {
                    return Results.Unauthorized();
                }

                // Add more claims here
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, request.Username),
                    new Claim(ClaimTypes.Name, request.Username)
                };

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // TODO: Make sure these are correct
                var authProperties = new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = true,
                };

                
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                var authResponse = 
                    await tokenService.GetToken(cancellationToken);

                // B2 tokens expire in 24 hours
                cache.Set(request.Username, new CacheModel(authResponse.Token, authResponse.DownloadBaseUrl, authResponse.ApiBaseUrl), DateTime.UtcNow.AddHours(24));
                return Results.Ok("success");
            });

            app.MapGet("/logout",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            });
        }
    }
}
