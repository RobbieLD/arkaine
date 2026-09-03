using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Server.Arkaine;
using Server.Arkaine.Admin;

namespace Server.Arkaine.Tests
{
    public class ProcessingReportServiceTests
    {
        [Test]
        public async Task Reports_AreListedNewestFirstAndCanBeDownloadedOrCleared()
        {
            await using var context = CreateContext();
            var service = new ProcessingReportService(context);
            var older = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
            var newer = older.AddMinutes(1);

            await service.SaveAsync(ProcessingReportType.Thumbnail, older, "<p>old</p>", CancellationToken.None);
            await service.SaveAsync(ProcessingReportType.Conversion, newer, "<p>new</p>", CancellationToken.None);

            var reports = await service.ListAsync(CancellationToken.None);

            Assert.That(reports.Select(report => report.Type), Is.EqualTo(new[] { "conversion", "thumbnail" }));
            Assert.That(reports.Select(report => report.Name), Is.EqualTo(new[]
            {
                "2026-09-03 09:01:00.000 UTC",
                "2026-09-03 09:00:00.000 UTC"
            }));

            var stored = await service.GetAsync(reports[0].Id, CancellationToken.None);
            Assert.That(stored?.Html, Is.EqualTo("<p>new</p>"));

            await service.ClearAsync(CancellationToken.None);

            Assert.That(await service.ListAsync(CancellationToken.None), Is.Empty);
        }

        [Test]
        public void Save_RejectsMissingHtml()
        {
            using var context = CreateContext();
            var service = new ProcessingReportService(context);

            Assert.That(
                async () => await service.SaveAsync(
                    ProcessingReportType.Thumbnail,
                    DateTimeOffset.UtcNow,
                    " ",
                    CancellationToken.None),
                Throws.ArgumentException);
        }

        private static ArkaineDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ArkaineDbContext>()
                .UseInMemoryDatabase($"processing-reports-{Guid.NewGuid():N}")
                .Options;
            return new ArkaineDbContext(options);
        }
    }
}
