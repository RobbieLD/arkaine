using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Server.Arkaine;
using Server.Arkaine.Favourites;

namespace Server.Arkaine.Tests
{
    public class FavouriteRepositoryTests
    {
        [Test]
        public async Task Remove_RemovesOnlyTheCurrentUsersFavourite()
        {
            await using var context = CreateContext();
            context.Favourites.AddRange(
                new Favourite { Name = "gallery/image.jpg", UserName = "user" },
                new Favourite { Name = "gallery/image.jpg", UserName = "admin" });
            await context.SaveChangesAsync();

            var repository = new FavouriteRepository(context);

            await repository.Remove("gallery/image.jpg", "user");
            await repository.Remove("missing.jpg", "user");
            Assert.That(await repository.All("user"), Is.Empty);
            Assert.That(await repository.All("admin"), Is.EqualTo(new[] { "gallery/image.jpg" }));
        }

        [Test]
        public async Task Page_OnlyReturnsFavouritesBelongingToTheRequestedUser()
        {
            await using var context = CreateContext();
            context.Favourites.AddRange(
                new Favourite { Name = "user/image.jpg", UserName = "user" },
                new Favourite { Name = "admin/image.jpg", UserName = "admin" });
            await context.SaveChangesAsync();

            var response = await new FavouriteRepository(context).Page("user", null, 10);

            Assert.That(response.Files.Select(file => file.FileName), Is.EqualTo(new[] { "user/image.jpg" }));
        }

        private static ArkaineDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ArkaineDbContext>()
                .UseInMemoryDatabase($"favourites-{Guid.NewGuid():N}")
                .Options;
            return new ArkaineDbContext(options);
        }
    }
}
