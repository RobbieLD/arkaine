using NUnit.Framework;
using Server.Arkaine.B2;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Arkaine.Tests
{
    public class MockB2Tests
    {
        private MockB2.Store _store = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new MockB2.Store();
            _store.Reset();
        }

        [Test]
        public async Task ListFiles_ReturnsFoldersAndVideosAtRoot()
        {
            var service = new MockB2(new ThumbnailInfoCache(), _store);
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
            var service = new MockB2(new ThumbnailInfoCache(), _store);
            var response = await service.ListFiles(
                new FilesRequest
                {
                    Delimiter = "/",
                    PageSize = 100,
                    Prefix = "gallery-gamma/"
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
            var service = new MockB2(new ThumbnailInfoCache(), _store);
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
            var service = new MockB2(cache, _store);

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
            var service = new MockB2(new ThumbnailInfoCache(), _store);
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
            var service = new MockB2(new ThumbnailInfoCache(), _store);
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

        [Test]
        public async Task ListFiles_HonorsExactFileNameFiltering()
        {
            var service = new MockB2(new ThumbnailInfoCache(), _store);

            var response = await service.ListFiles(
                new FilesRequest
                {
                    ExactFileName = "gallery-alpha/animated.webp",
                    PageSize = 100
                },
                "test-user",
                null,
                CancellationToken.None);

            Assert.That(response.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-alpha/animated.webp" }));
            Assert.That(response.NextFileName, Is.Empty);
        }

        [Test]
        public async Task UploadSingleFile_ThenDownloadAndDelete_MutatesTheSharedStore()
        {
            var service = new MockB2(new ThumbnailInfoCache(), _store);
            var payload = new byte[] { 1, 2, 3, 4 };

            await using (var upload = new MemoryStream(payload))
            {
                await service.UploadSingleFile("uploads/new-image.jpg", "image/jpeg", payload.Length, upload, CancellationToken.None);
            }

            await using (var stream = await service.Download("test-user", "uploads/new-image.jpg", CancellationToken.None))
            await using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                Assert.That(buffer.ToArray(), Is.EqualTo(payload));
            }

            var uploaded = _store.SnapshotFiles().Single(file => file.FileName == "uploads/new-image.jpg");
            await service.Delete(new DeleteModel { FileName = uploaded.FileName, Id = uploaded.Id }, CancellationToken.None);

            Assert.That(_store.SnapshotFiles().Any(file => file.FileName == "uploads/new-image.jpg"), Is.False);
        }

        [Test]
        public void Stores_ResetIndependently()
        {
            var first = new MockB2.Store();
            var second = new MockB2.Store();
            first.Reset([new MockB2.MockB2Object("first.jpg", "image/jpeg", [1])]);
            second.Reset([new MockB2.MockB2Object("second.jpg", "image/jpeg", [2])]);

            first.Reset([new MockB2.MockB2Object("updated.jpg", "image/jpeg", [3])]);

            Assert.That(first.SnapshotFiles().Select(file => file.FileName), Is.EqualTo(new[] { "updated.jpg" }));
            Assert.That(second.SnapshotFiles().Select(file => file.FileName), Is.EqualTo(new[] { "second.jpg" }));
        }
    }
}
