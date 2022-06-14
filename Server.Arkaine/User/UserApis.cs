using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Server.Arkaine.B2;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.Arkaine.User
{
    public static class UserApis
    {
        public static void RegisterUserApis(this WebApplication app, WebApplicationBuilder builder)
        {
            app.MapPost("/login",
                [AllowAnonymous]
            async (LoginRequest request, IUserService service) =>
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return Results.BadRequest("Username and password are required");
                }
                
                var roles = await service.LoginUserAsync(request.Username, request.Password);
                if (roles == null)
                {
                    return Results.Unauthorized();
                }

                // Add more claims here
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, request.Username)
                };

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var token = new JwtSecurityToken(
                    issuer: builder.Configuration["JWT_ISSUER"],
                    audience: builder.Configuration["JWT_AUDIENCE"],
                    claims: claims,
                    expires: app.Environment.IsDevelopment() ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddMinutes(5),
                    notBefore: DateTime.UtcNow,
                    signingCredentials: new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_KEY"])),
                        SecurityAlgorithms.HmacSha256));

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return Results.Ok(tokenString);
            });
        }
    }
}
