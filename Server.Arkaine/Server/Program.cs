using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using Server.Arkaine;
using Server.Arkaine.B2;
using Server.Arkaine.User;
using System.Net;

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
    options.LogoutPath = new PathString("/#/login");
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

// We only need CORS for development
#if DEBUG
if (builder.Configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: cors, policy =>
        {
            policy.WithOrigins("http://localhost:8080")
                .WithHeaders(HeaderNames.ContentType, HeaderNames.Accept)
                .WithMethods("GET", "POST")
                .AllowCredentials();
        });
    });
}
#endif

var app = builder.Build();

var cookiePolicy = new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.None,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
};

app.UseIPFilter(IPAddress.Parse(builder.Configuration["ACCEPT_IP_RANGE"]));

if (app.Environment.IsDevelopment())
{
    app.UseCors(cors);
}

app.UseCookiePolicy(cookiePolicy);
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseExceptionHandler("/error");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/status", () => "Server is running");
app.MapGet("/error", () => "There was a server error");
app.MapGet("/forbidden", () => "You do not have access to this page");

app.RegisterUserApis();
app.RegisterB2Apis();

//await SeedUser.Initialize(app.Services);

app.Run();
