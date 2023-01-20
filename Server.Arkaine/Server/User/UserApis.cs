using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using Server.Arkaine.Notification;
using System.Data;
using System.Security.Claims;
using System.Threading;

namespace Server.Arkaine.User
{
    public static class UserApis
    {
        public static void RegisterUserApis(this WebApplication app)
        {
            app.MapGet("/loggedin",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            (HttpContext context) =>
            {
                return Results.Ok(context?.User?.Identity?.Name);
            });

            app.MapPost("/twofactorauth",
                [AllowAnonymous]
            async (
                    TwoFactorRequest request,
                    HttpContext context,
                    CancellationToken cancellationToken,
                    IOptions<ArkaineOptions> config,
                    IUserService userService,
                    IB2Service b2Service,
                    IMemoryCache cache,
                    INotifier notifier) =>
            {
                var roles = await userService.TwoFactorAuthenticateAsync(request.Code, request.Username);

                if (roles == null)
                {
                    await notifier.Send($"{request.Username} failed to login due to incorrect auth code");
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
                    //AllowRefresh = true,
                    //IsPersistent = true,
                };

                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                var authResponse = await b2Service.GetToken(config.Value.B2_KEY_READ, cancellationToken);
                
                // B2 tokens expire in 24 hours
                cache.Set(request.Username,
                    new CacheModel(authResponse.Token, authResponse.DownloadBaseUrl, authResponse.ApiBaseUrl, authResponse.AccountId),
                    DateTime.UtcNow.AddHours(24));
                await notifier.Send($"{request.Username} Successfully logged in");
                return Results.Ok(request.Username);
            });

            app.MapPost("/login",
                [AllowAnonymous]
            async (
                    LoginRequest request,
                    HttpContext context,
                    CancellationToken cancellationToken,
                    IUserService userService,
                    INotifier notifier) =>
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    await notifier.Send($"{request.Username} failed to login because no password was supplied");
                    return Results.BadRequest("Username and password are required");
                }
                
                var passwordCorrect = await userService.LoginUserAsync(request.Username, request.Password);
                if (!passwordCorrect)
                {
                    await notifier.Send($"{request.Username} failed to sign in");
                    return Results.Unauthorized();
                }

                return Results.Ok();                
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
