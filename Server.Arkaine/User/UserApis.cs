using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Arkaine.B2;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.Arkaine.User
{
    public static class UserApis
    {
        public static void RegisterUserApis(this WebApplication app)
        {
            app.MapPost("/login",
                [AllowAnonymous]
            async (LoginRequest request, CancellationToken cancellationToken, IOptions<ArkaineOptions> config, IUserService userService, IB2Service tokenService, IMemoryCache cache) =>
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

                var token = new JwtSecurityToken(
                    issuer: config.Value.JWT_ISSUER,
                    audience: config.Value.JWT_AUDIENCE,
                    claims: claims,
                    expires: app.Environment.IsDevelopment() ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddMinutes(15),
                    notBefore: DateTime.UtcNow,
                    signingCredentials: new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.JWT_KEY)),
                        SecurityAlgorithms.HmacSha256));

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // Generate a user token and store it in cache
                var authResponse = 
                    await tokenService.GetToken(cancellationToken);

                // B2 tokens expire in 24 hours
                cache.Set(request.Username, new CacheModel(authResponse.Token, authResponse.DownloadBaseUrl, authResponse.ApiBaseUrl), DateTime.UtcNow.AddHours(24));
                return Results.Ok(tokenString);
            });
        }
    }
}
