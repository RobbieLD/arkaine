using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Server.Arkaine;
using Server.Arkaine.B2;
using Server.Arkaine.Ingest;
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
    options.Cookie.MaxAge = options.ExpireTimeSpan;
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/forbidden";
    options.LoginPath = new PathString("/login");
    options.EventsType = typeof(CustomCookieAuthenticationEvent);
});

var lifetimeKey = Guid.NewGuid();
builder.Services.Configure<ArkaineOptions>(config);
builder.Services.AddTransient<CustomCookieAuthenticationEvent>(s => 
    ActivatorUtilities.CreateInstance<CustomCookieAuthenticationEvent>(s, config["MAX_COOKIE_LIFETIME"], lifetimeKey));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IB2Service, B2Service>();
builder.Services.AddScoped<SgExtractor>();
builder.Services.AddScoped<EchoExtractor>();
builder.Services.AddScoped<IExtractorFactory, ExtractorFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ArkaineDbContext>(options => options.UseNpgsql(builder.Configuration["DB_CONNECTION_STRING"]));
builder.Services.AddAuthorization();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
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
app.UseForwardedHeaders();

var cookiePolicy = new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    MinimumSameSitePolicy = app.Environment.IsDevelopment() ?
        Microsoft.AspNetCore.Http.SameSiteMode.None :
        Microsoft.AspNetCore.Http.SameSiteMode.Strict,
    Secure = app.Environment.IsDevelopment() ?
        CookieSecurePolicy.SameAsRequest :
        CookieSecurePolicy.Always
};

 if (!app.Environment.IsDevelopment())
{
    app.UseIPFilter(builder.Configuration["ACCEPT_IP_RANGE"].Split(",").Select(ip => IPAddress.Parse(ip)));
    app.UserSecurityHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors(cors);
}

app.UseCookiePolicy(cookiePolicy);
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
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
app.RegisterIngestApis();

//await SeedUser.Initialize(app.Services);

app.Run();
