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
var dev = builder.Environment.IsDevelopment();

IConfiguration config = builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", true)
    .AddEnvironmentVariables()
    .Build();
var trustedProxyAddresses = ParseIpAddresses(config["TRUSTED_PROXY_IPS"], "TRUSTED_PROXY_IPS");
var allowedIpAddresses = dev
    ? Array.Empty<IPAddress>()
    : ParseIpAddresses(config["ACCEPT_IP_RANGE"], "ACCEPT_IP_RANGE");
var configuredPasskeyOrigins = ParseOrigins(
    string.IsNullOrWhiteSpace(config["CORS_ORIGIN"]) && dev
        ? "http://localhost:8081"
        : config["CORS_ORIGIN"]);
Action<CookieAuthenticationOptions> configureAuthenticationCookie = options =>
{
    options.Cookie.SameSite = dev
        ? Microsoft.AspNetCore.Http.SameSiteMode.None
        : Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
};

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
    configureAuthenticationCookie(options);
});

builder.Services.Configure<ArkaineOptions>(config);
builder.Services.AddSingleton<IBackgroundTaskQueue, UploadQueue>();
builder.Services.AddScoped<GlobalExceptionHandler>();
builder.Services.AddScoped(s => ActivatorUtilities.CreateInstance<CustomCookieAuthenticationEvent>(
    s,
    config["MAX_COOKIE_LIFETIME"] ?? throw new("Cookie Lifetime Must Be Set")));
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
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
});
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ValidateOrigin = context =>
    {
        var credentialOrigin = NormalizeOrigin(context.Origin);
        var requestOrigin = NormalizeOrigin(context.HttpContext.Request.Headers.Origin.ToString());
        var serverOrigin = NormalizeOrigin(
            $"{context.HttpContext.Request.Scheme}://{context.HttpContext.Request.Host}");

        var validOrigin = !context.CrossOrigin &&
            credentialOrigin is not null &&
            string.Equals(credentialOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(credentialOrigin, serverOrigin, StringComparison.OrdinalIgnoreCase) ||
             configuredPasskeyOrigins.Any(origin =>
                 string.Equals(origin, credentialOrigin, StringComparison.OrdinalIgnoreCase)));

        return ValueTask.FromResult(validOrigin);
    };
});
builder.Services.ConfigureApplicationCookie(configureAuthenticationCookie);
builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorUserIdScheme,
    configureAuthenticationCookie);
builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorRememberMeScheme,
    configureAuthenticationCookie);

// We only need CORS for development
#if DEBUG
if (dev)
{
    var corsOrigins = string.IsNullOrWhiteSpace(config["CORS_ORIGIN"])
        ? new[] { "http://localhost:8081" }
        : config["CORS_ORIGIN"]!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: cors, policy =>
        {
            policy.WithOrigins(corsOrigins)
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
app.RegisterProfileApis();
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

static IReadOnlyList<string> ParseOrigins(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Array.Empty<string>();
    }

    var origins = new List<string>();
    foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var origin = NormalizeOrigin(item)
            ?? throw new InvalidOperationException("CORS_ORIGIN contains an invalid origin.");
        origins.Add(origin);
    }

    return origins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

static string? NormalizeOrigin(string? value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        string.IsNullOrEmpty(uri.Host) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        (uri.AbsolutePath is not "" and not "/") ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
        return null;
    }

    var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
    return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{port}";
}
