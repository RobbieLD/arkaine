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
                "-maxrate",
                "8000000",
                "-bufsize",
                "16000000",
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
        public async Task ConvertAsync_ReportsRemoteHttpStatusFromFfmpeg()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(
                1,
                string.Empty,
                "[http] HTTP error 401 Unauthorized",
                TimeSpan.Zero,
                false,
                false));
            var converter = CreateConverter(runner);

            var result = await converter.ConvertAsync(
                new MediaConversionRequest(
                    "https://download.example/video.mp4?Authorization=scoped-token",
                    "target.jpg",
                    MediaConversionKind.Image,
                    TimeSpan.FromSeconds(12)),
                CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.HttpStatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task ProbeAsync_ParsesVideoAndAudioMetadata()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(
                0,
                """
                {
                  "format": { "duration": "120.5", "size": "50000000", "bit_rate": "12000000" },
                  "streams": [
                    {
                      "codec_type": "video",
                      "codec_name": "h264",
                      "bit_rate": "10000000",
                      "width": 3840,
                      "height": 2160,
                      "avg_frame_rate": "30000/1001"
                    },
                    {
                      "codec_type": "audio",
                      "codec_name": "aac",
                      "bit_rate": "192000"
                    }
                  ]
                }
                """,
                string.Empty,
                TimeSpan.Zero,
                false,
                false));
            var converter = CreateConverter(runner);

            var result = await converter.ProbeAsync(
                "https://download.example/video.mp4?Authorization=scoped-token",
                CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.Duration, Is.EqualTo(TimeSpan.FromSeconds(120.5)));
            Assert.That(result.Metadata.FormatBitrate, Is.EqualTo(12_000_000));
            Assert.That(result.Metadata.VideoBitrate, Is.EqualTo(10_000_000));
            Assert.That(result.Metadata.VideoCodec, Is.EqualTo("h264"));
            Assert.That(result.Metadata.FrameRate, Is.EqualTo(30000d / 1001d).Within(0.0001));
            Assert.That(result.Metadata.AudioBitrate, Is.EqualTo(192_000));
            Assert.That(result.Metadata.FileSize, Is.EqualTo(50_000_000));
            Assert.That(runner.Calls[0].FileName, Is.EqualTo("ffprobe"));
            Assert.That(runner.Calls[0].Arguments, Is.EqualTo(new[]
            {
                "-v",
                "error",
                "-print_format",
                "json",
                "-show_format",
                "-show_streams",
                "https://download.example/video.mp4?Authorization=scoped-token"
            }));
        }

        [Test]
        public async Task ProbeAsync_ReturnsUnknownMetadataWhenBitrateIsMissing()
        {
            var runner = new QueueProcessRunner();
            runner.Enqueue(new ProcessRunResult(
                0,
                """{"format":{"duration":"2"},"streams":[{"codec_type":"video","codec_name":"vp9"}]}""",
                string.Empty,
                TimeSpan.Zero,
                false,
                false));
            var converter = CreateConverter(runner);

            var result = await converter.ProbeAsync("video.webm", CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata!.VideoBitrate, Is.Null);
            Assert.That(result.Metadata.FormatBitrate, Is.Null);
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
