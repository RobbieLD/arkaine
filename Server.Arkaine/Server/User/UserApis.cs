using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Server.Arkaine.B2;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
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
                var response = new LoginResponse(context?.User?.Identity?.Name ?? throw new("User not found"), context?.User?.IsInRole("Admin") ?? false);
                return Results.Ok(response);
            });

            app.MapPost("/twofactorauth",
                [AllowAnonymous]
            async (
                    TwoFactorRequest request,
                    HttpContext context,
                    CancellationToken cancellationToken,
                    IOptions<ArkaineOptions> config,
                    IUserService userService,
                    UserManager<IdentityUser> userManager,
                    IB2Service b2Service,
                    IMemoryCache cache) =>
            {
                var user = await userService.TwoFactorAuthenticateAsync(request.Code, request.Remember);

                if (user == null)
                {
                    return Results.Unauthorized();
                }

                var username = user.UserName ?? throw new("User not found");
                var roles = await userManager.GetRolesAsync(user);

                // Add more claims here
                var userId = await userManager.GetUserIdAsync(user);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, username)
                };

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = request.Remember,
                };

                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                var authResponse = await b2Service.GetToken(config.Value.B2_KEY_READ, cancellationToken);
                
                // B2 tokens expire in 24 hours
                cache.Set(username,
                    new CacheModel(authResponse.Token, authResponse.DownloadBaseUrl, authResponse.ApiBaseUrl, authResponse.AccountId),
                    DateTime.UtcNow.AddHours(23));

                var response = new LoginResponse(username, roles.Contains("Admin"));

                return Results.Ok(response);
            });

            app.MapPost("/passkeys/options",
                [AllowAnonymous]
            async (
                    PasskeyRequestOptionsRequest request,
                    UserManager<IdentityUser> userManager,
                    SignInManager<IdentityUser> signInManager) =>
            {
                IdentityUser? user = null;
                if (!string.IsNullOrWhiteSpace(request.Username))
                {
                    user = await userManager.FindByNameAsync(request.Username.Trim());
                    if (user is null)
                    {
                        return Results.Unauthorized();
                    }
                }

                var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
                return Results.Content(optionsJson, "application/json");
            });

            app.MapPost("/passkeys/login",
                [AllowAnonymous]
            async (
                    PasskeyLoginRequest request,
                    IUserService userService,
                    ILoggerFactory loggerFactory) =>
            {
                if (request.Credential.ValueKind != JsonValueKind.Object)
                {
                    return Results.BadRequest(new { message = "A passkey credential is required." });
                }

                var credentialJson = request.Credential.GetRawText();
                if (credentialJson.Length > PasskeyLimits.MaxCredentialJsonLength)
                {
                    return Results.BadRequest(new { message = "The passkey credential is too large." });
                }

                SignInResult signInResult;
                try
                {
                    signInResult = await userService.PasskeyLoginAsync(credentialJson, request.Remember);
                }
                catch (InvalidOperationException exception)
                {
                    loggerFactory
                        .CreateLogger("Server.Arkaine.User.UserApis")
                        .LogWarning(exception, "Rejected passkey login with invalid ceremony state.");
                    return Results.BadRequest(new { message = "Passkey login failed." });
                }

                if (!signInResult.Succeeded)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(false);
            });

            app.MapPost("/login",
                [AllowAnonymous]
            async (
                    LoginRequest request,
                    IUserService userService) =>
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return Results.BadRequest("Username and password are required");
                }
                
                var signInResult = await userService.LoginUserAsync(request.Username, request.Password, request.Remember);
                if (signInResult.Succeeded)
                {
                    return Results.Ok(false);
                }

                if (signInResult.RequiresTwoFactor)
                {
                    return Results.Ok(true);
                }
                else
                {
                    return Results.Unauthorized();
                }
            });

            app.MapGet("/logout",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Cookies.Delete(".AspNetCore.Identity.Application");
                context.Response.Cookies.Delete("Identity.TwoFactorRememberMe");
            });
        }
    }
}
