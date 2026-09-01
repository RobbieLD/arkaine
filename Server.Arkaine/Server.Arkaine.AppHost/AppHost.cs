var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-username", "admin", publishValueAsDefault: true);
var postgres = builder.AddPostgres("postgres", userName: postgresUser)
    .WithHostPort(5432)
    .WithDataVolume("postgres-data")
    .WithEndpointProxySupport(false);
var database = postgres.AddDatabase("arkaine-db", "arkaine");

var migrations = builder.AddProject<Projects.Server_Arkaine_Migrations>("database-migrations")
    .WithEnvironment("DB_CONNECTION_STRING", database)
    .WaitFor(database);

var localB2Root = builder.Configuration["LOCAL_B2_ROOT"] ?? "data";
var localB2BucketId = builder.Configuration["LOCAL_B2_BUCKET_ID"] ?? "local-bucket";
var localB2BucketName = builder.Configuration["LOCAL_B2_BUCKET_NAME"] ?? "local";
var localB2 = builder.AddProject<Projects.Server_Arkaine_LocalB2>("local-b2")
    .WithHttpEndpoint()
    .WithEnvironment("LOCAL_B2_ROOT", localB2Root)
    .WithEnvironment("LOCAL_B2_BUCKET_ID", localB2BucketId)
    .WithEnvironment("LOCAL_B2_BUCKET_NAME", localB2BucketName)
    .WithExternalHttpEndpoints();

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
    .WithEnvironment("B2AuthUrl", $"{localB2.GetEndpoint("http")}/b2api/v2/b2_authorize_account")
    .WithEnvironment("B2_KEY_READ", "local-read")
    .WithEnvironment("B2_KEY_WRITE", "local-write")
    .WithEnvironment("BUCKET_ID", localB2BucketId)
    .WithEnvironment("BUCKET_NAME", localB2BucketName)
    .WaitFor(localB2)
    .WaitForCompletion(migrations);

var client = builder.AddViteApp("client", "../../Client.Arkaine")
    .WithYarn(installArgs: new[] { "--frozen-lockfile" })
    // Not VITE_ prefixed on purpose: this only configures the dev server's proxy, it must
    // never reach client code. The client always calls the API on its own origin.
    .WithEnvironment("ARKAINE_SERVER_URL", arkaine.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(arkaine);

arkaine.WithEnvironment("CORS_ORIGIN", client.GetEndpoint("http"));

builder.Build().Run();
