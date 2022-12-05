using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Server.Arkaine.Migrations
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArkaineDbContext>
    {
        public ArkaineDbContext CreateDbContext(string[] args)
        {
            string path = Directory.GetCurrentDirectory();

            IConfigurationBuilder builder =
                new ConfigurationBuilder()
                    .SetBasePath(path)
                    .AddJsonFile("appsettings.json");

            IConfigurationRoot config = builder.Build();

            string connectionString = config["DB_CONNECTION_STRING"];

            Console.WriteLine($"DesignTimeDbContextFactory: using base path = {path}");
            Console.WriteLine($"DesignTimeDbContextFactory: using connection string = {connectionString}");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Could not find connection string named 'DB_CONNECTION_STRING'");
            }

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<ArkaineDbContext>();

            ArkaineDbContext.AddBaseOptions(dbContextOptionsBuilder, connectionString);

            dbContextOptionsBuilder.UseNpgsql(b => b.MigrationsAssembly("Server.Arkaine.Migrations"));

            return new ArkaineDbContext(dbContextOptionsBuilder.Options);
        }
    }
}
