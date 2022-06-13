﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Server.Arkaine
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArkaineDbContext>
    {
        public ArkaineDbContext CreateDbContext(string[] args)
        {
            string path = Directory.GetCurrentDirectory();
            
            IConfigurationBuilder builder =
                new ConfigurationBuilder()
                    .SetBasePath(path)
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile("appsettings.local.json", true);

            IConfigurationRoot config = builder.Build();

            string connectionString = config.GetConnectionString("Heroku");

            Console.WriteLine($"DesignTimeDbContextFactory: using base path = {path}");
            Console.WriteLine($"DesignTimeDbContextFactory: using connection string = {connectionString}");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Could not find connection string named 'Heroku'");
            }

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<ArkaineDbContext>();

            ArkaineDbContext.AddBaseOptions(dbContextOptionsBuilder, connectionString);

            return new ArkaineDbContext(dbContextOptionsBuilder.Options);
        }
    }
}
