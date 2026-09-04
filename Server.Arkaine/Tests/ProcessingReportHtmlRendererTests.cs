using NUnit.Framework;
using Server.Arkaine.Admin;

namespace Server.Arkaine.Tests
{
    public class ProcessingReportHtmlRendererTests
    {
        [Test]
        public void RenderThumbnail_IncludesSummaryAndEscapedFailureDetails()
        {
            var html = ProcessingReportHtmlRenderer.RenderThumbnail(new GenerationReport
            {
                Status = "completed",
                Scanned = 2,
                Generated = 1,
                Failed = 1,
                Failures =
                [
                    new ThumbnailFailure(
                        "folder/<image>.webp",
                        "file-1",
                        "upload",
                        "image/webp",
                        "42",
                        "invalid <data>")
                ]
            });

            Assert.That(html, Does.Contain("<h2>Summary</h2>"));
            Assert.That(html, Does.Contain("folder/&lt;image&gt;.webp"));
            Assert.That(html, Does.Contain("invalid &lt;data&gt;"));
        }

        [Test]
        public void RenderConversion_IncludesEachOutcomeAndErrorDetails()
        {
            var html = ProcessingReportHtmlRenderer.RenderConversion(new ConversionReport
            {
                Status = "completed",
                Files =
                [
                    new ConversionFileResult(
                        "folder/source.webp",
                        "folder/source.jpg",
                        "converted",
                        "file-1",
                        "upload",
                        "image/webp",
                        "100",
                        string.Empty),
                    new ConversionFileResult(
                        "folder/failed.webp",
                        "folder/failed.jpg",
                        "error",
                        "file-2",
                        "upload",
                        "image/webp",
                        "200",
                        "ffmpeg failed")
                ]
            });

            Assert.That(html, Does.Contain("folder/source.webp"));
            Assert.That(html, Does.Contain("converted"));
            Assert.That(html, Does.Contain("folder/failed.jpg"));
            Assert.That(html, Does.Contain("ffmpeg failed"));
        }

        [Test]
        public void RenderConversion_ExcludesSkippedFilesButKeepsSkippedSummary()
        {
            var html = ProcessingReportHtmlRenderer.RenderConversion(new ConversionReport
            {
                Status = "completed",
                Skipped = 1,
                Files =
                [
                    new ConversionFileResult(
                        "folder/already-supported.mp4",
                        string.Empty,
                        "skipped",
                        "file-1",
                        "upload",
                        "video/mp4",
                        "100",
                        "The file type is already supported."),
                    new ConversionFileResult(
                        "folder/source.webp",
                        "folder/source.jpg",
                        "converted",
                        "file-2",
                        "upload",
                        "image/webp",
                        "200",
                        string.Empty)
                ]
            });

            Assert.That(html, Does.Contain("Skipped"));
            Assert.That(html, Does.Contain("folder/source.webp"));
            Assert.That(html, Does.Not.Contain("folder/already-supported.mp4"));
            Assert.That(html, Does.Not.Contain("The file type is already supported."));
        }
    }
}
