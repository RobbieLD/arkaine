// https://dev.to/alrobilliard/deploying-net-core-to-heroku-1lfe
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Server.Arkaine.User;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateActor = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwd:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddAuthorization();
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapPost("/login",
    async (LoginRequest request, IUserService service) => await Login(request, service));

app.MapGet("/token",
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
    async (string bucket, IUserService service) => await GetB2Token(bucket, service));

app.MapGet("/",
    () => "Server is running");

async Task<string> GetB2Token(string bucket, IUserService service)
{
    return await service.GetB2Token(bucket);
}

async Task<IResult> Login(LoginRequest request, IUserService service)
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
    {
        return Results.BadRequest("Username and password are required");
    }

    var loggedInUser = await service.LoginUserAsync(request.Username, request.Password);

    if (loggedInUser == null)
    {
        return Results.Unauthorized();
    }

    // Add more claims here
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, request.Username)
    };

    var token = new JwtSecurityToken(
        issuer: builder.Configuration["Jwt:Issuer"],
        audience: builder.Configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(5),
        notBefore: DateTime.UtcNow,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            SecurityAlgorithms.HmacSha256));

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(tokenString);
}

app.Run();
