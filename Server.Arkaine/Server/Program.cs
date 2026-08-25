using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
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
var trustedProxyAddresses = ParseIpAddresses(config["TRUSTED_PROXY_IPS"], "TRUSTED_PROXY_IPS");
var allowedIpAddresses = dev
    ? Array.Empty<IPAddress>()
    : ParseIpAddresses(config["ACCEPT_IP_RANGE"], "ACCEPT_IP_RANGE");

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
builder.Services.Configure<HttpClientFactoryOptions>(Options.DefaultName, options =>
{
    options.HttpMessageHandlerBuilderActions.Add(builder =>
    {
        builder.PrimaryHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = UrlSafetyValidator.ConnectAsync
        };
    });
});
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
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;

    foreach (var address in trustedProxyAddresses)
    {
        options.KnownProxies.Add(address);
    }
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
if (trustedProxyAddresses.Count > 0)
{
    app.UseForwardedHeaders();
}

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
    app.UseIPFilter(allowedIpAddresses);
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

var seedDb = false;
var seedDbSetting = config["SEED_DB"];
if (!string.IsNullOrWhiteSpace(seedDbSetting) && !bool.TryParse(seedDbSetting, out seedDb))
{
    throw new InvalidOperationException("SEED_DB must be set to true or false.");
}

if (seedDb)
{
    if (!dev)
    {
        throw new InvalidOperationException("SEED_DB is only supported in the Development environment.");
    }

    await SeedUser.Initialize(app.Services);
}
    
app.Run();

static IReadOnlyList<IPAddress> ParseIpAddresses(string? value, string settingName)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Array.Empty<IPAddress>();
    }

    var addresses = new List<IPAddress>();
    foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!IPAddress.TryParse(item, out var address))
        {
            throw new InvalidOperationException($"{settingName} contains an invalid IP address.");
        }

        addresses.Add(address);
    }

    return addresses;
}
