using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Server.Arkaine;
using Server.Arkaine.Migrations;

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING is required.");
}

var optionsBuilder = new DbContextOptionsBuilder<ArkaineDbContext>();
var identityServiceCollection = new ServiceCollection();
identityServiceCollection
    .AddOptions<IdentityOptions>()
    .Configure(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
var identityServices = identityServiceCollection.BuildServiceProvider();
optionsBuilder.UseApplicationServiceProvider(identityServices);
ArkaineDbContext.AddBaseOptions(optionsBuilder, connectionString);
optionsBuilder.UseNpgsql(options => options.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name));

Console.WriteLine("Applying database migrations...");

await using var dbContext = new ArkaineDbContext(optionsBuilder.Options);
await dbContext.Database.MigrateAsync();

Console.WriteLine("Database migrations complete.");
