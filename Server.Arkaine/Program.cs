using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Server.Arkaine;
using Server.Arkaine.B2;
using Server.Arkaine.User;

var builder = WebApplication.CreateBuilder(args);
var cors = "arkaineCors";

IConfiguration config = builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", true)
    .AddEnvironmentVariables()
    .Build();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/forbidden";
});

builder.Services.Configure<ArkaineOptions>(config);
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IB2Service, B2Service>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ArkaineDbContext>(options => options.UseNpgsql(builder.Configuration["DB_CONNECTION_STRING"]));
builder.Services.AddAuthorization();
builder.Services.AddDefaultIdentity<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ArkaineDbContext>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: cors, policy => 
    {
        policy.WithOrigins(builder.Configuration["CORS_ORIGIN"])
            .WithHeaders(HeaderNames.ContentType, HeaderNames.Accept)
            .WithMethods("GET", "POST")
            .AllowCredentials();
    });
});

var cookiePolicy = new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.None,
    Secure = CookieSecurePolicy.Always
};

var app = builder.Build();
app.UseCors(cors);
app.UseCookiePolicy(cookiePolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.MapGet("/", () => "Server is running");
app.MapGet("/error", () => "There was a server error");
app.MapGet("/forbidden", () => "You do not have access to this page");

app.RegisterUserApis();
app.RegisterB2Apis();

//await SeedUser.Initialize(app.Services);

app.Run();
