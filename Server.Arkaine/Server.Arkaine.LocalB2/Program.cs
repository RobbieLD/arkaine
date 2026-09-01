using Server.Arkaine.LocalB2;

var builder = WebApplication.CreateBuilder(args);
var configuredRoot = builder.Configuration["LOCAL_B2_ROOT"]
                    ?? builder.Configuration["LocalB2:RootDirectory"];
var options = new LocalB2Options
{
    RootDirectory = string.IsNullOrWhiteSpace(configuredRoot)
        ? Path.Combine(builder.Environment.ContentRootPath, "data")
        : Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(builder.Environment.ContentRootPath, configuredRoot),
    BucketId = builder.Configuration["LOCAL_B2_BUCKET_ID"]
               ?? builder.Configuration["LocalB2:BucketId"]
               ?? "local-bucket",
    BucketName = builder.Configuration["LOCAL_B2_BUCKET_NAME"]
                 ?? builder.Configuration["LocalB2:BucketName"]
                 ?? "local"
};
options.Normalize();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<LocalB2Store>();

var app = builder.Build();
app.MapLocalB2();

app.Run();

public partial class Program;
