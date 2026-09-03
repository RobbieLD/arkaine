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
using Server.Arkaine.Media;
using Server.Arkaine.Tags;
using Server.Arkaine.User;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var cors = "arkaineCors";
var dev = builder.Environment.IsDevelopment();
var localUrl = "http://localhost:8081";

IConfiguration config = builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", true)
    .AddEnvironmentVariables()
    .Build();

var b2AuthHost = Uri.TryCreate(config["B2AuthUrl"], UriKind.Absolute, out var b2AuthUri) &&
                 b2AuthUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
    ? b2AuthUri.DnsSafeHost
    : null;
var proxyHostName = config["TRUSTED_PROXY"];

var proxyIpAddresses = string.IsNullOrEmpty(proxyHostName) ? [] : Dns.GetHostAddresses(proxyHostName!);

var configuredPasskeyOrigins = ParseOrigins(
    string.IsNullOrWhiteSpace(config["CORS_ORIGIN"]) && dev
        ? localUrl
        : config["CORS_ORIGIN"]);
Action<CookieAuthenticationOptions> configureAuthenticationCookie = options =>
{
    // Development serves the client through the Vite dev-server proxy, so the browser is
    // already on the same origin as the API over plain http on localhost. Demanding
    // SameSite=None there would also demand Secure, and the browser would drop the cookie.
    options.Cookie.SameSite = dev
        ? SameSiteMode.Lax
        : SameSiteMode.Strict;
    options.Cookie.SecurePolicy = dev
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
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
builder.Services.PostConfigure<ArkaineOptions>(options => options.Normalize());
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
builder.Services.AddHttpClient<B2Service>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = (context, cancellationToken) =>
            UrlSafetyValidator.ConnectAsync(context, cancellationToken, b2AuthHost)
    });
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<SgExtractor>();
builder.Services.AddSingleton<AdminJobCoordinator>();
builder.Services.AddSingleton<ThumbnailManager>();
builder.Services.AddSingleton<ConversionManager>();
builder.Services.AddScoped<WhExtractor>();
builder.Services.AddScoped<IfExtractor>();
builder.Services.AddScoped<EchoExtractor>();
builder.Services.AddScoped<LrExtractor>();
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IFavouritesService, FavouritesService>();
builder.Services.AddScoped<IProcessingReportService, ProcessingReportService>();
builder.Services.AddScoped<IExtractorFactory, ExtractorFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ArkaineDbContext>(options => options.UseNpgsql(builder.Configuration["DB_CONNECTION_STRING"]));
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHostedService<UploadService>();
builder.Services.AddSingleton<IThumbnailInfoProvider, ThumbnailInfoCache>();
builder.Services.AddSingleton<IProcessRunner, SystemProcessRunner>();
builder.Services.AddSingleton<IMediaConverter, FfmpegMediaConverter>();
builder.Services.AddScoped<IB2Service>(services => services.GetRequiredService<B2Service>());

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

    foreach (var address in proxyIpAddresses)
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
        ? [localUrl]
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
app.Services.GetRequiredService<IOptions<ArkaineOptions>>().Value.Validate();
if (proxyIpAddresses?.Length > 0)
{
    app.UseForwardedHeaders();
}

var cookiePolicy = new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    MinimumSameSitePolicy = app.Environment.IsDevelopment() ?
        SameSiteMode.None :
        SameSiteMode.Strict,
    Secure = app.Environment.IsDevelopment() ?
        CookieSecurePolicy.SameAsRequest :
        CookieSecurePolicy.Always
};

 if (!app.Environment.IsDevelopment())
{
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
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.GetTypedHeaders();

        // Vite fingerprints everything under /assets, so those files can never change
        // content without changing name. index.html must always be revalidated or the
        // client would keep booting a stale bundle.
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(365),
                Extensions = { new Microsoft.Net.Http.Headers.NameValueHeaderValue("immutable") }
            };
        }
        else
        {
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true
            };
        }
    }
});
app.UseMiddleware<GlobalExceptionHandler>();
app.MapGet("/status", () => "Server is running");
app.MapGet("/error", () => "There was a server error");
app.MapGet("/forbidden", () => "You do not have access to this page");

app.RegisterUserApis();
app.RegisterProfileApis();
app.RegisterB2Apis();
// Removing these as they are not currently used.
//app.RegisterIngestApis();
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
