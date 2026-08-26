using NUnit.Framework;
using Server.Arkaine.B2;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Arkaine.Tests
{
    public class MockB2Tests
    {
        [Test]
        public async Task ListFiles_ReturnsFoldersAndVideosAtRoot()
        {
            var service = new MockB2(new ThumbnailInfoCache());
            var response = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 100
                },
                "test-user",
                null,
                CancellationToken.None);

            Assert.That(response.Files.Count(file => file.Type == "folder"), Is.GreaterThanOrEqualTo(3));
            Assert.That(response.Files.Count(file => file.ContentType == "video/mp4"), Is.EqualTo(3));
            Assert.That(response.Files.Count(file => file.ContentType == "audio/mpeg"), Is.EqualTo(3));
            Assert.That(response.Files.Any(file => file.FileName == "test.mp4"), Is.True);
            Assert.That(response.Files.Any(file => file.FileName == "test.mp3"), Is.True);
        }

        [Test]
        public async Task ListFiles_ReturnsImagesInsideFolder()
        {
            var service = new MockB2(new ThumbnailInfoCache());
            var root = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 100
                },
                "test-user",
                null,
                CancellationToken.None);
            var folder = root.Files.First(file => file.Type == "folder");
            Assert.That(folder.ChildCount, Is.GreaterThan(0));

            var response = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 100,
                    Prefix = folder.FileName
                },
                "test-user",
                null,
                CancellationToken.None);

            Assert.That(response.Files, Is.Not.Empty);
            Assert.That(response.Files.All(file => file.ContentType == "image/jpeg"), Is.True);
        }

        [Test]
        public async Task ListFiles_HonorsPaging()
        {
            var service = new MockB2(new ThumbnailInfoCache());
            var firstPage = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 1
                },
                "test-user",
                null,
                CancellationToken.None);

            var secondPage = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 1,
                    StartFile = firstPage.NextFileName
                },
                "test-user",
                null,
                CancellationToken.None);

            Assert.That(firstPage.Files, Has.Count.EqualTo(1));
            Assert.That(firstPage.NextFileName, Is.Not.Empty);
            Assert.That(secondPage.Files, Has.Count.EqualTo(1));
            Assert.That(secondPage.Files[0].FileName, Is.Not.EqualTo(firstPage.Files[0].FileName));
        }

        [Test]
        public async Task ListFiles_PopulatesPreviewsThroughTheDimensionCache()
        {
            var cache = new ThumbnailInfoCache();
            var service = new MockB2(cache);

            var root = await service.ListFiles(
                new FilesRequest { Delimiter = "/", PageSize = 100 },
                "test-user",
                null,
                CancellationToken.None);

            var folder = root.Files.First(file => file.Type == "folder");
            var stats = cache.GetStats();

            Assert.That(stats.Entries, Is.GreaterThan(0), "the mock service must exercise the dimension cache");
            Assert.That(folder.Thumbnail, Is.Not.Empty);
            Assert.That(folder.PreviewWidth, Is.GreaterThan(0));
            Assert.That(folder.PreviewHeight, Is.GreaterThan(0));

            // Audio has no thumbnail, so it resolves to nothing and is counted as such.
            var audio = root.Files.First(file => file.ContentType == "audio/mpeg");
            Assert.That(audio.Thumbnail, Is.Empty);
            Assert.That(audio.PreviewWidth, Is.Null);
            Assert.That(stats.NotFound, Is.GreaterThan(0));

            // Listing the same folder again must be served from the cache.
            var before = cache.GetStats().Hits;
            await service.ListFiles(
                new FilesRequest { Delimiter = "/", PageSize = 100 },
                "test-user",
                null,
                CancellationToken.None);

            Assert.That(cache.GetStats().Hits, Is.GreaterThan(before));
        }

        [Test]
        public async Task Preview_ServesTheGeneratedThumbnailFromDisk()
        {
            var service = new MockB2(new ThumbnailInfoCache());
            var root = await service.ListFiles(
                new FilesRequest { Delimiter = "/", PageSize = 100 },
                "test-user",
                null,
                CancellationToken.None);

            var folder = root.Files.First(file => file.Type == "folder");

            // A physical file result means the real thumbnail is served rather than the
            // single fallback image, so the dimensions match what the listing advertised.
            Assert.That(
                service.Preview(folder.Thumbnail).GetType().Name,
                Is.EqualTo("PhysicalFileHttpResult"));
        }

        [Test]
        public async Task ListFiles_GivesImagesVariedAspectRatios()
        {
            var service = new MockB2(new ThumbnailInfoCache());
            var root = await service.ListFiles(
                new FilesRequest { Delimiter = "/", PageSize = 100 },
                "test-user",
                null,
                CancellationToken.None);

            var folder = root.Files.First(file => file.Type == "folder");
            var images = await service.ListFiles(
                new FilesRequest { Delimiter = "/", PageSize = 100, Prefix = folder.FileName },
                "test-user",
                null,
                CancellationToken.None);

            var ratios = images.Files
                .Where(file => file.PreviewWidth > 0 && file.PreviewHeight > 0)
                .Select(file => (double)file.PreviewWidth!.Value / file.PreviewHeight!.Value)
                .Distinct()
                .ToList();

            Assert.That(ratios.Count, Is.GreaterThan(1), "the gallery needs varied ratios to show masonry");
        }
    }
}
