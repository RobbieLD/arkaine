using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Arkaine;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Ingest;
using Server.Arkaine.Notification;
using Server.Arkaine.Tags;
using Server.Arkaine.User;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var cors = "arkaineCors";
var dev = builder.Configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

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
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/forbidden";
    options.LoginPath = new PathString("/login");
    options.EventsType = typeof(CustomCookieAuthenticationEvent);
    options.ExpireTimeSpan = TimeSpan.FromDays(int.Parse(config["MAX_COOKIE_LIFETIME"] ?? throw new("Cookie Lifetime Must Be Set")));
});

var lifetimeKey = Guid.NewGuid();
builder.Services.Configure<ArkaineOptions>(config);
builder.Services.AddSingleton<IBackgroundTaskQueue, UploadQueue>();
builder.Services.AddScoped<GlobalExceptionHandler>();
builder.Services.AddScoped(s => ActivatorUtilities.CreateInstance<CustomCookieAuthenticationEvent>(
    s,
    config["MAX_COOKIE_LIFETIME"] ?? throw new("Cookie Lifetime Must Be Set"),
    lifetimeKey));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<INotifier>(s => ActivatorUtilities.CreateInstance<Pushover>(s, dev));
builder.Services.AddScoped<SgExtractor>();
builder.Services.AddSingleton<ThumbnailManager>();
builder.Services.AddScoped<WhExtractor>();
builder.Services.AddScoped<IfExtractor>();
builder.Services.AddScoped<EchoExtractor>();
builder.Services.AddScoped<LrExtractor>();
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IFavouritesService, FavouritesService>();
builder.Services.AddScoped<IExtractorFactory, ExtractorFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ArkaineDbContext>(options => options.UseNpgsql(builder.Configuration["DB_CONNECTION_STRING"]));
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHostedService<UploadService>();

if (!string.IsNullOrEmpty(builder.Configuration["MOCK_B2"]))
{
    builder.Services.AddScoped<IB2Service, MockB2>();
}
else
{
    builder.Services.AddScoped<IB2Service, B2Service>();
}

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
if (dev)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: cors, policy =>
        {
            policy.WithOrigins("http://localhost:8081")
                .AllowAnyHeader()
                .AllowAnyMethod()
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
    app.UseIPFilter(builder.Configuration["ACCEPT_IP_RANGE"]?.Split(",")?.Select(ip => IPAddress.Parse(ip)) ?? new List<IPAddress>());
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
app.UseMiddleware<GlobalExceptionHandler>();
app.MapGet("/status", () => "Server is running");
app.MapGet("/error", () => "There was a server error");
app.MapGet("/forbidden", () => "You do not have access to this page");

app.RegisterUserApis();
app.RegisterB2Apis();
app.RegisterIngestApis();
app.RegisterAdminApis();
app.RegisterFavouritesApis();
app.RegisterTagApis();

if (!string.IsNullOrEmpty(builder.Configuration["SEED_DB"]))
{
    await SeedUser.Initialize(app.Services);
}
    
app.Run();
