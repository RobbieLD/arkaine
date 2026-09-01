using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Media;

namespace Server.Arkaine.Tests
{
    public class FfmpegMediaConverterTests
    {
        [Test]
        public async Task ConvertAsync_BuildsFirstFrameJpegArguments()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
            var converter = CreateConverter(runner);

            var result = await converter.ConvertAsync(
                new MediaConversionRequest("source.webp", "target.jpg", MediaConversionKind.Image, TimeSpan.FromSeconds(12)),
                CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(runner.Calls, Has.Count.EqualTo(1));
            Assert.That(runner.Calls[0].Arguments, Is.EqualTo(new[]
            {
                "-hide_banner",
                "-nostdin",
                "-y",
                "-loglevel",
                "error",
                "-i",
                "source.webp",
                "-map",
                "0:v:0",
                "-frames:v",
                "1",
                "-q:v",
                "2",
                "target.jpg"
            }));
        }

        [Test]
        public async Task ConvertAsync_BuildsH264Mp4Arguments()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
            var converter = CreateConverter(runner);

            var result = await converter.ConvertAsync(
                new MediaConversionRequest("source.mov", "target.mp4", MediaConversionKind.Video, TimeSpan.FromSeconds(12)),
                CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(runner.Calls[0].Arguments, Is.EqualTo(new[]
            {
                "-hide_banner",
                "-nostdin",
                "-y",
                "-loglevel",
                "error",
                "-i",
                "source.mov",
                "-map",
                "0:v:0",
                "-map",
                "0:a?",
                "-vf",
                "scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p",
                "-c:v",
                "libx264",
                "-crf",
                "23",
                "-preset",
                "medium",
                "-pix_fmt",
                "yuv420p",
                "-color_range",
                "tv",
                "-c:a",
                "aac",
                "-b:a",
                "128k",
                "-movflags",
                "+faststart",
                "target.mp4"
            }));
        }

        [Test]
        public async Task GetAvailabilityAsync_ReportsMissingEncoders()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(0, "ffmpeg version 7.0", string.Empty, TimeSpan.Zero, false, false));
            runner.Enqueue(new ProcessRunResult(0, "Encoders:\n V..... mjpeg", string.Empty, TimeSpan.Zero, false, false));
            var converter = CreateConverter(runner);

            var availability = await converter.GetAvailabilityAsync(CancellationToken.None);

            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(availability.SupportsImageConversion, Is.True);
            Assert.That(availability.SupportsVideoConversion, Is.False);
            Assert.That(availability.MissingEncoders, Is.EquivalentTo(new[] { "libx264", "aac" }));
        }

        [Test]
        public async Task GetAvailabilityAsync_RetriesAfterTransientTimeout()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(-1, string.Empty, "timeout", TimeSpan.FromSeconds(5), true, false));
            runner.Enqueue(new ProcessRunResult(0, "ffmpeg version 7.0", string.Empty, TimeSpan.Zero, false, false));
            runner.Enqueue(new ProcessRunResult(0, "Encoders:\n V..... libx264\n A..... aac", string.Empty, TimeSpan.Zero, false, false));
            var converter = CreateConverter(runner);

            var first = await converter.GetAvailabilityAsync(CancellationToken.None);
            var second = await converter.GetAvailabilityAsync(CancellationToken.None);

            Assert.That(first.IsAvailable, Is.False);
            Assert.That(second.IsAvailable, Is.True);
            Assert.That(runner.Calls, Has.Count.EqualTo(3));
        }

        private static FfmpegMediaConverter CreateConverter(QueueProcessRunner runner)
        {
            var options = TestOptionsFactory.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n")));
            return new FfmpegMediaConverter(Options.Create(options), runner, NullLogger<FfmpegMediaConverter>.Instance);
        }
    }
}
