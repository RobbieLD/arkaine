using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Server.Arkaine.Admin;

namespace Server.Arkaine.Tests
{
    public class VideoConversionRequestStoreTests
    {
        [Test]
        public async Task EnqueueAsync_UpdatesExistingFileAndPendingQueriesExcludeCompletedItems()
        {
            await using var context = CreateContext();
            var store = new VideoConversionRequestStore(context);

            var first = await store.EnqueueAsync(
                "gallery/video.mp4",
                "file-01",
                "admin",
                VideoConversionRequestReason.Manual,
                CancellationToken.None);
            await store.MarkRunningAsync(first.Id, CancellationToken.None);
            Assert.That(await store.GetPendingAsync("gallery/", CancellationToken.None), Has.Count.EqualTo(1));
            await store.MarkCompletedAsync(first.Id, CancellationToken.None);

            var second = await store.EnqueueAsync(
                "gallery/video.mp4",
                "file-01",
                "admin",
                VideoConversionRequestReason.Manual,
                CancellationToken.None);

            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(second.Status, Is.EqualTo(VideoConversionRequestStatus.Queued));
            Assert.That(await store.GetPendingAsync("gallery/", CancellationToken.None), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CancelAsync_OnlyCancelsQueuedRequests()
        {
            await using var context = CreateContext();
            var store = new VideoConversionRequestStore(context);

            var request = await store.EnqueueAsync(
                "video.mp4",
                "file-01",
                "admin",
                VideoConversionRequestReason.Manual,
                CancellationToken.None);

            Assert.That(await store.CancelAsync(request.Id, CancellationToken.None), Is.True);
            Assert.That(await store.CancelAsync(request.Id, CancellationToken.None), Is.False);
            Assert.That(await store.ListAsync(VideoConversionRequestStatus.Cancelled, CancellationToken.None), Has.Count.EqualTo(1));
        }

        private static ArkaineDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ArkaineDbContext>()
                .UseInMemoryDatabase($"requests-{Guid.NewGuid():N}")
                .Options;
            return new ArkaineDbContext(options);
        }
    }
}
