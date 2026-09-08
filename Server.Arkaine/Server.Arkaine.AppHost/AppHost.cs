var builder = DistributedApplication.CreateBuilder(args);

var useRealB2 = string.Equals(
    builder.Configuration["B2_BACKEND"],
    "real",
    StringComparison.OrdinalIgnoreCase);
var postgresUser = builder.AddParameter("postgres-username", "admin", publishValueAsDefault: true);
var postgres = builder.AddPostgres("postgres", userName: postgresUser)
    .WithHostPort(5432)
    .WithDataVolume("postgres-data")
    .WithEndpointProxySupport(false);
var database = postgres.AddDatabase("arkaine-db", "arkaine");

var migrations = builder.AddProject<Projects.Server_Arkaine_Migrations>("database-migrations")
    .WithEnvironment("DB_CONNECTION_STRING", database)
    .WaitFor(database);

var realThumbnailDirectory = useRealB2
    ? ResolvePath(
        builder.Configuration["THUMBNAIL_DIR"],
        builder.Environment.ContentRootPath,
        "thumbnails")
    : string.Empty;

builder.AddContainer("adminer", "michalhosna/adminer")
    .WithEnvironment("ADMINER_DB", "arkaine")
    .WithEnvironment("ADMINER_DRIVER", "pgsql")
    .WithEnvironment("ADMINER_PASSWORD", postgres.Resource.PasswordParameter)
    .WithEnvironment("ADMINER_SERVER", "postgres")
    .WithEnvironment("ADMINER_USERNAME", postgresUser)
    .WithEnvironment("ADMINER_AUTOLOGIN", "1")
    .WithEnvironment("ADMINER_NAME", "Arkaine Development Database")
    .WithReference(database)
    .WithHttpEndpoint(targetPort: 8080, port: 8080)
    .WithEndpointProxySupport(false)
    .WaitFor(database);

var arkaine = builder.AddProject<Projects.Server_Arkaine>("arkaine")
    .WithEnvironment("DB_CONNECTION_STRING", database)
    .WithEnvironment("MAX_COOKIE_LIFETIME", builder.Configuration["MAX_COOKIE_LIFETIME"] ?? "30")
    .WaitForCompletion(migrations);

if (useRealB2)
{
    arkaine
        .WithEnvironment("B2AuthUrl", GetRequiredSetting("B2AuthUrl"))
        .WithEnvironment("B2_KEY_READ", GetRequiredSetting("B2_KEY_READ"))
        .WithEnvironment("B2_KEY_WRITE", GetRequiredSetting("B2_KEY_WRITE"))
        .WithEnvironment("BUCKET_ID", GetRequiredSetting("BUCKET_ID"))
        .WithEnvironment("BUCKET_NAME", GetRequiredSetting("BUCKET_NAME"))
        .WithEnvironment("THUMBNAIL_DIR", realThumbnailDirectory);
}
else
{
    var localB2ProjectDirectory = Path.GetFullPath(
        Path.Combine(builder.Environment.ContentRootPath, "..", "Server.Arkaine.LocalB2"));
    var localB2Root = ResolvePath(
        builder.Configuration["LOCAL_B2_ROOT"],
        localB2ProjectDirectory,
        "data");
    var localB2ThumbnailDirectory = Path.Combine(
        Directory.GetParent(localB2Root)?.FullName
            ?? throw new InvalidOperationException("The local B2 data directory must have a parent directory."),
        "thumbnails");
    var localB2BucketId = builder.Configuration["LOCAL_B2_BUCKET_ID"] ?? "local-bucket";
    var localB2BucketName = builder.Configuration["LOCAL_B2_BUCKET_NAME"] ?? "local";
    var storageSetup = builder.AddProject<Projects.Server_Arkaine_StorageSetup>("local-storage-setup")
        .WithEnvironment("LOCAL_B2_ROOT", localB2Root)
        .WithEnvironment("THUMBNAIL_DIR", localB2ThumbnailDirectory);
    var localB2 = builder.AddProject<Projects.Server_Arkaine_LocalB2>("local-b2")
        .WithHttpEndpoint()
        .WithEnvironment("LOCAL_B2_ROOT", localB2Root)
        .WithEnvironment("LOCAL_B2_BUCKET_ID", localB2BucketId)
        .WithEnvironment("LOCAL_B2_BUCKET_NAME", localB2BucketName)
        .WaitForCompletion(storageSetup)
        .WithExternalHttpEndpoints();

    arkaine
        .WithEnvironment("B2AuthUrl", $"{localB2.GetEndpoint("http")}/b2api/v2/b2_authorize_account")
        .WithEnvironment("B2_KEY_READ", "local-read")
        .WithEnvironment("B2_KEY_WRITE", "local-write")
        .WithEnvironment("BUCKET_ID", localB2BucketId)
        .WithEnvironment("BUCKET_NAME", localB2BucketName)
        .WithEnvironment("THUMBNAIL_DIR", localB2ThumbnailDirectory)
        .WaitFor(localB2)
        .WaitForCompletion(storageSetup);
}

var client = builder.AddViteApp("client", "../../Client.Arkaine")
    .WithYarn(installArgs: new[] { "--frozen-lockfile" })
    // Not VITE_ prefixed on purpose: this only configures the dev server's proxy, it must
    // never reach client code. The client always calls the API on its own origin.
    .WithEnvironment("ARKAINE_SERVER_URL", arkaine.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(arkaine);

arkaine.WithEnvironment("CORS_ORIGIN", client.GetEndpoint("http"));

builder.Build().Run();

static string ResolvePath(string? configuredPath, string relativeBase, string defaultPath)
{
    var path = string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath.Trim();
    return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(relativeBase, path));
}

string GetRequiredSetting(string name)
{
    var value = builder.Configuration[name];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"B2_BACKEND=real requires the {name} setting in AppHost user secrets or environment variables.");
    }

    return value;
}
