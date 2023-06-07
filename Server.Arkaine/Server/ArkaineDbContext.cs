using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Server.Arkaine.Favourites;
using Server.Arkaine.Tags;

namespace Server.Arkaine
{
    public partial class ArkaineDbContext : IdentityDbContext<IdentityUser>
    {
        public ArkaineDbContext(DbContextOptions<ArkaineDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }

        public static void AddBaseOptions(DbContextOptionsBuilder<ArkaineDbContext> builder, string connectionString)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string must be provided", nameof(connectionString));

            builder.UseNpgsql(connectionString, x =>
            {
                x.EnableRetryOnFailure();
            });
        }

        public DbSet<Favourite> Favourites => Set<Favourite>();
        public DbSet<Tag> Tags => Set<Tag>();
    }
}
