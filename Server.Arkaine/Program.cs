using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Arkaine;
using Server.Arkaine.Identity;
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
        ValidIssuer = builder.Configuration["JWT_ISSUER"],
        ValidAudience = builder.Configuration["JWT_AUDIENCE"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_KEY"]))
    };
});

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", true)
    .AddEnvironmentVariables();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddDbContext<ArkaineDbContext>(options => options.UseNpgsql(builder.Configuration["DB_CONNECTION_STRING"]));
builder.Services.AddAuthorization();
builder.Services.AddDefaultIdentity<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ArkaineDbContext>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapPost("/login",
    async (LoginRequest request, IUserService service) => await Login(request, service));

app.MapGet("/token",
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User, Admin")]
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

    var isSignedIn = await service.LoginUserAsync(request.Username, request.Password);

    if (!isSignedIn)
    {
        return Results.Unauthorized();
    }

    // Add more claims here
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, request.Username)
    };

    var token = new JwtSecurityToken(
        issuer: builder.Configuration["JWT_ISSUER"],
        audience: builder.Configuration["JWT_AUDIENCE"],
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(5),
        notBefore: DateTime.UtcNow,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_KEY"])),
            SecurityAlgorithms.HmacSha256));

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(tokenString);
}

//await SeedUser.Initialize(app.Services);

app.Run();
