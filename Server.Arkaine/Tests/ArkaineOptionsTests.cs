using NUnit.Framework;
using Server.Arkaine.B2;

namespace Server.Arkaine.Tests
{
    public class ArkaineOptionsTests
    {
        [Test]
        public void Normalize_NormalizesConfiguredExtensionLists()
        {
            var options = new ArkaineOptions
            {
                THUMBNAIL_EXTENSIONS = " JPG ; .webp png ",
                IMAGE_EXTENSIONS = "webp;AVIF",
                VIDEO_EXTENSIONS = " mov ;AVI ",
                FFMPEG_PATH = "  ffmpeg.exe  "
            };

            options.Normalize();

            Assert.That(options.THUMBNAIL_EXTENSIONS, Is.EqualTo(".jpg,.png,.webp"));
            Assert.That(options.IMAGE_EXTENSIONS, Is.EqualTo(".avif,.webp"));
            Assert.That(options.VIDEO_EXTENSIONS, Is.EqualTo(".avi,.mov"));
            Assert.That(options.FFMPEG_PATH, Is.EqualTo("ffmpeg.exe"));
        }

        [Test]
        public void Validate_UsesThumbnailPageSizeWhenConversionPageSizeIsUnset()
        {
            var options = new ArkaineOptions
            {
                PAGE_SIZE = "25",
                THUMBNAIL_PAGE_SIZE = 25,
                THUMBNAIL_WIDTH = 350,
                UPLOAD_CHUNK_SIZE = B2MultipartLimits.MinimumPartSizeBytes,
                CONVERT_PAGE_SIZE = 0,
                FFMPEG_PROBE_TIMEOUT_SECONDS = 5,
                FFMPEG_CONVERSION_TIMEOUT_SECONDS = 30
            };

            options.Validate();

            Assert.That(options.CONVERT_PAGE_SIZE, Is.EqualTo(options.THUMBNAIL_PAGE_SIZE));
        }

        [Test]
        public void GetConversionTargetFileName_MapsConfiguredImageAndVideoExtensions()
        {
            var options = new ArkaineOptions
            {
                IMAGE_EXTENSIONS = ".webp",
                VIDEO_EXTENSIONS = ".avi,.mov"
            };

            options.Normalize();

            Assert.That(options.GetConversionTargetFileName("gallery\\still.webp"), Is.EqualTo("gallery\\still.jpg"));
            Assert.That(options.GetConversionTargetFileName("gallery/video.avi"), Is.EqualTo("gallery/video_compressed.mp4"));
            Assert.That(options.GetConversionTargetFileName("gallery/ready.mp4"), Is.EqualTo("gallery/ready.mp4"));
        }

        [Test]
        public void Validate_RejectsNegativeConversionSizesAndTimeouts()
        {
            var options = new ArkaineOptions
            {
                CONVERT_PAGE_SIZE = -1,
                THUMBNAIL_PAGE_SIZE = 25,
                THUMBNAIL_WIDTH = 350,
                UPLOAD_CHUNK_SIZE = B2MultipartLimits.MinimumPartSizeBytes,
                PAGE_SIZE = "25"
            };

            Assert.That(() => options.Validate(), Throws.InvalidOperationException);
        }

        [Test]
        public void Validate_RejectsMultipartPartsBelowB2Minimum()
        {
            var options = new ArkaineOptions
            {
                CONVERT_PAGE_SIZE = 25,
                THUMBNAIL_PAGE_SIZE = 25,
                THUMBNAIL_WIDTH = 350,
                UPLOAD_CHUNK_SIZE = B2MultipartLimits.MinimumPartSizeBytes - 1,
                PAGE_SIZE = "25"
            };

            Assert.That(() => options.Validate(), Throws.InvalidOperationException
                .With.Message.Contains(nameof(options.UPLOAD_CHUNK_SIZE)));
        }
    }
}
