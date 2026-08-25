using Microsoft.EntityFrameworkCore;
using Server.Arkaine;
using Server.Arkaine.Migrations;

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DB_CONNECTION_STRING is required.");
}

var optionsBuilder = new DbContextOptionsBuilder<ArkaineDbContext>();
ArkaineDbContext.AddBaseOptions(optionsBuilder, connectionString);
optionsBuilder.UseNpgsql(options => options.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name));

Console.WriteLine("Applying database migrations...");

await using var dbContext = new ArkaineDbContext(optionsBuilder.Options);
await dbContext.Database.MigrateAsync();

Console.WriteLine("Database migrations complete.");
