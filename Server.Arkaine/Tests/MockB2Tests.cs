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
            var service = new MockB2();
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
            var service = new MockB2();
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
            var service = new MockB2();
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
    }
}
