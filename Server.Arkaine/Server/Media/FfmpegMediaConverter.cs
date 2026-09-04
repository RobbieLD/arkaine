using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Arkaine.Media
{
    public class FfmpegMediaConverter : IMediaConverter
    {
        private readonly ArkaineOptions _options;
        private readonly IProcessRunner _runner;
        private readonly ILogger<FfmpegMediaConverter> _logger;
        private readonly SemaphoreSlim _availabilityLock = new(1, 1);
        private MediaConverterAvailability? _availability;

        public FfmpegMediaConverter(
            IOptions<ArkaineOptions> options,
            IProcessRunner runner,
            ILogger<FfmpegMediaConverter> logger)
        {
            _options = options.Value;
            _options.Normalize();
            _runner = runner;
            _logger = logger;
        }

        public async Task<MediaConverterAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
        {
            if (_availability is not null)
            {
                return _availability;
            }

            await _availabilityLock.WaitAsync(cancellationToken);
            try
            {
                if (_availability is null)
                {
                    var probe = await ProbeAvailabilityAsync(cancellationToken);
                    if (probe.Cacheable)
                    {
                        _availability = probe.Availability;
                    }

                    return probe.Availability;
                }

                return _availability;
            }
            finally
            {
                _availabilityLock.Release();
            }
        }

        private async Task<AvailabilityProbeResult> ProbeAvailabilityAsync(CancellationToken cancellationToken)
        {
            try
            {
                var timeout = TimeSpan.FromSeconds(_options.FFMPEG_PROBE_TIMEOUT_SECONDS);
                var versionProcess = CreateBareStartInfo();
                versionProcess.ArgumentList.Add("-version");

                var versionResult = await _runner.RunAsync(versionProcess, timeout, cancellationToken);
                if (versionResult.TimedOut || versionResult.Cancelled || versionResult.ExitCode != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new AvailabilityProbeResult(
                        new MediaConverterAvailability(
                            false,
                            false,
                            false,
                            _options.FFMPEG_PATH,
                            string.Empty,
                            [],
                            BuildFailure(versionResult, "ffmpeg -version")),
                        !versionResult.TimedOut && !versionResult.Cancelled);
                }

                var encodersProcess = CreateBareStartInfo();
                encodersProcess.ArgumentList.Add("-hide_banner");
                encodersProcess.ArgumentList.Add("-encoders");

                var encoderResult = await _runner.RunAsync(encodersProcess, timeout, cancellationToken);
                if (encoderResult.TimedOut || encoderResult.Cancelled || encoderResult.ExitCode != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new AvailabilityProbeResult(
                        new MediaConverterAvailability(
                            false,
                            true,
                            false,
                            _options.FFMPEG_PATH,
                            ExtractFirstLine(versionResult.StandardOutput),
                            ["libx264", "aac"],
                            BuildFailure(encoderResult, "ffmpeg -encoders")),
                        !encoderResult.TimedOut && !encoderResult.Cancelled);
                }

                var missingEncoders = new List<string>();
                if (!ContainsEncoder(encoderResult.StandardOutput, "libx264"))
                {
                    missingEncoders.Add("libx264");
                }

                if (!ContainsEncoder(encoderResult.StandardOutput, "aac"))
                {
                    missingEncoders.Add("aac");
                }

                return new AvailabilityProbeResult(
                    new MediaConverterAvailability(
                        missingEncoders.Count == 0,
                        true,
                        missingEncoders.Count == 0,
                        _options.FFMPEG_PATH,
                        ExtractFirstLine(versionResult.StandardOutput),
                        missingEncoders,
                        string.Empty),
                    true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Win32Exception exception)
            {
                _logger.LogWarning(exception, "ffmpeg was not available at {ExecutablePath}", _options.FFMPEG_PATH);
                return new AvailabilityProbeResult(
                    new MediaConverterAvailability(false, false, false, _options.FFMPEG_PATH, string.Empty, ["libx264", "aac"], exception.Message),
                    true);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "ffmpeg availability probe failed.");
                return new AvailabilityProbeResult(
                    new MediaConverterAvailability(false, false, false, _options.FFMPEG_PATH, string.Empty, ["libx264", "aac"], exception.Message),
                    false);
            }
        }

        public async Task<MediaConversionResult> ConvertAsync(MediaConversionRequest request, CancellationToken cancellationToken)
        {
            var timeout = request.Timeout > TimeSpan.Zero
                ? request.Timeout
                : TimeSpan.FromSeconds(request.Kind == MediaConversionKind.Image
                    ? _options.CONVERT_IMAGE_TIMEOUT_SECONDS
                    : _options.CONVERT_VIDEO_TIMEOUT_SECONDS);
            var process = CreateProcessStartInfo(request);
            var result = await _runner.RunAsync(process, timeout, cancellationToken);

            return new MediaConversionResult(
                !result.TimedOut && !result.Cancelled && result.ExitCode == 0,
                result.ExitCode,
                result.StandardError,
                result.Duration,
                result.TimedOut,
                result.Cancelled,
                ExtractHttpStatusCode(result.StandardError));
        }

        public async Task<MediaMetadataResult> ProbeAsync(
            string sourcePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("A media source must be supplied.", nameof(sourcePath));
            }

            var process = CreateProbeStartInfo(sourcePath);
            ProcessRunResult result;
            try
            {
                result = await _runner.RunAsync(
                    process,
                    TimeSpan.FromSeconds(_options.FFMPEG_PROBE_TIMEOUT_SECONDS),
                    cancellationToken);
            }
            catch (Win32Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "ffprobe was not available at {ExecutablePath}",
                    _options.FFPROBE_PATH);
                return new MediaMetadataResult(
                    false,
                    null,
                    exception.Message,
                    TimeSpan.Zero,
                    false,
                    false);
            }

            if (result.Cancelled || cancellationToken.IsCancellationRequested)
            {
                return new MediaMetadataResult(
                    false,
                    null,
                    "Media metadata probing was cancelled.",
                    result.Duration,
                    false,
                    true,
                    ExtractHttpStatusCode(result.StandardError));
            }

            if (result.TimedOut)
            {
                return new MediaMetadataResult(
                    false,
                    null,
                    "Media metadata probing timed out.",
                    result.Duration,
                    true,
                    false,
                    ExtractHttpStatusCode(result.StandardError));
            }

            if (result.ExitCode != 0)
            {
                return new MediaMetadataResult(
                    false,
                    null,
                    BuildFailure(result, "ffprobe"),
                    result.Duration,
                    false,
                    false,
                    ExtractHttpStatusCode(result.StandardError));
            }

            try
            {
                return new MediaMetadataResult(
                    true,
                    ParseMetadata(result.StandardOutput),
                    string.Empty,
                    result.Duration,
                    false,
                    false,
                    ExtractHttpStatusCode(result.StandardError));
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "ffprobe returned invalid metadata for {SourcePath}",
                    RedactDownloadAuthorization(sourcePath));
                return new MediaMetadataResult(
                    false,
                    null,
                    "ffprobe returned invalid media metadata.",
                    result.Duration,
                    false,
                    false,
                    ExtractHttpStatusCode(result.StandardError));
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "ffprobe returned unusable metadata for {SourcePath}",
                    RedactDownloadAuthorization(sourcePath));
                return new MediaMetadataResult(
                    false,
                    null,
                    exception.Message,
                    result.Duration,
                    false,
                    false,
                    ExtractHttpStatusCode(result.StandardError));
            }
        }

        internal ProcessStartInfo CreateProcessStartInfo(MediaConversionRequest request)
        {
            var process = CreateBareStartInfo();
            process.ArgumentList.Add("-hide_banner");
            process.ArgumentList.Add("-nostdin");
            process.ArgumentList.Add("-y");
            process.ArgumentList.Add("-loglevel");
            process.ArgumentList.Add("error");
            process.ArgumentList.Add("-i");
            process.ArgumentList.Add(request.SourcePath);

            if (request.Kind == MediaConversionKind.Image)
            {
                process.ArgumentList.Add("-map");
                process.ArgumentList.Add("0:v:0");
                process.ArgumentList.Add("-frames:v");
                process.ArgumentList.Add("1");
                process.ArgumentList.Add("-q:v");
                process.ArgumentList.Add(_options.CONVERT_IMAGE_QUALITY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                process.ArgumentList.Add(request.TargetPath);
                return process;
            }

            process.ArgumentList.Add("-map");
            process.ArgumentList.Add("0:v:0");
            process.ArgumentList.Add("-map");
            process.ArgumentList.Add("0:a?");
            process.ArgumentList.Add("-vf");
            process.ArgumentList.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p");
            process.ArgumentList.Add("-c:v");
            process.ArgumentList.Add("libx264");
            process.ArgumentList.Add("-crf");
            process.ArgumentList.Add(_options.CONVERT_VIDEO_CRF.ToString(System.Globalization.CultureInfo.InvariantCulture));
            process.ArgumentList.Add("-preset");
            process.ArgumentList.Add(_options.CONVERT_VIDEO_PRESET);
            process.ArgumentList.Add("-maxrate");
            process.ArgumentList.Add(_options.CONVERT_VIDEO_MAX_BITRATE.ToString(CultureInfo.InvariantCulture));
            process.ArgumentList.Add("-bufsize");
            process.ArgumentList.Add(GetVideoBufferSize().ToString(CultureInfo.InvariantCulture));
            process.ArgumentList.Add("-pix_fmt");
            process.ArgumentList.Add("yuv420p");
            process.ArgumentList.Add("-color_range");
            process.ArgumentList.Add("tv");
            process.ArgumentList.Add("-c:a");
            process.ArgumentList.Add("aac");
            process.ArgumentList.Add("-b:a");
            process.ArgumentList.Add(_options.CONVERT_VIDEO_AUDIO_BITRATE);
            process.ArgumentList.Add("-movflags");
            process.ArgumentList.Add("+faststart");
            process.ArgumentList.Add(request.TargetPath);
            return process;
        }

        internal ProcessStartInfo CreateProbeStartInfo(string sourcePath)
        {
            var process = new ProcessStartInfo
            {
                FileName = _options.FFPROBE_PATH,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            process.ArgumentList.Add("-v");
            process.ArgumentList.Add("error");
            process.ArgumentList.Add("-print_format");
            process.ArgumentList.Add("json");
            process.ArgumentList.Add("-show_format");
            process.ArgumentList.Add("-show_streams");
            process.ArgumentList.Add(sourcePath);
            return process;
        }

        private ProcessStartInfo CreateBareStartInfo()
        {
            return new ProcessStartInfo
            {
                FileName = _options.FFMPEG_PATH,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
        }

        private static bool ContainsEncoder(string output, string encoder)
        {
            return output.Contains($" {encoder}", StringComparison.Ordinal) ||
                   output.Contains($"\t{encoder}", StringComparison.Ordinal) ||
                   output.Contains(encoder, StringComparison.Ordinal);
        }

        private static string ExtractFirstLine(string value)
        {
            using var reader = new StringReader(value);
            return reader.ReadLine()?.Trim() ?? string.Empty;
        }

        private static int? ExtractHttpStatusCode(string error)
        {
            const string marker = "HTTP error ";
            var markerIndex = error.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            var statusStart = markerIndex + marker.Length;
            return statusStart + 3 <= error.Length &&
                   int.TryParse(error.AsSpan(statusStart, 3), out var statusCode)
                ? statusCode
                : null;
        }

        private static MediaMetadata ParseMetadata(string output)
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            var format = root.TryGetProperty("format", out var formatElement)
                ? formatElement
                : default;
            var streams = root.TryGetProperty("streams", out var streamsElement) &&
                          streamsElement.ValueKind == JsonValueKind.Array
                ? streamsElement.EnumerateArray().ToList()
                : [];
            var video = streams.FirstOrDefault(stream =>
                ReadString(stream, "codec_type")?.Equals("video", StringComparison.OrdinalIgnoreCase) == true);
            var audio = streams.FirstOrDefault(stream =>
                ReadString(stream, "codec_type")?.Equals("audio", StringComparison.OrdinalIgnoreCase) == true);

            if (video.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("ffprobe did not find a video stream.");
            }

            return new MediaMetadata(
                ReadDuration(format),
                ReadInt64(format, "bit_rate"),
                ReadInt64(video, "bit_rate"),
                ReadString(video, "codec_name") ?? string.Empty,
                ReadInt32(video, "width"),
                ReadInt32(video, "height"),
                ReadFrameRate(video),
                audio.ValueKind == JsonValueKind.Undefined ? null : ReadInt64(audio, "bit_rate"),
                audio.ValueKind == JsonValueKind.Undefined
                    ? string.Empty
                    : ReadString(audio, "codec_name") ?? string.Empty,
                ReadInt64(format, "size"));
        }

        private long GetVideoBufferSize()
        {
            return _options.CONVERT_VIDEO_MAX_BITRATE > long.MaxValue / 2
                ? long.MaxValue
                : _options.CONVERT_VIDEO_MAX_BITRATE * 2;
        }

        private static TimeSpan? ReadDuration(JsonElement format)
        {
            var duration = ReadDouble(format, "duration");
            return duration is > 0
                ? TimeSpan.FromSeconds(duration.Value)
                : null;
        }

        private static double? ReadFrameRate(JsonElement stream)
        {
            var value = ReadString(stream, "avg_frame_rate") ?? ReadString(stream, "r_frame_rate");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var parts = value.Split('/', 2);
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var frameRate)
                ? frameRate
                : null;
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.ToString();
        }

        private static long? ReadInt64(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt64(out var number))
            {
                return number;
            }

            return long.TryParse(
                property.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : null;
        }

        private static int? ReadInt32(JsonElement element, string propertyName)
        {
            var value = ReadInt64(element, propertyName);
            return value is >= int.MinValue and <= int.MaxValue
                ? (int)value.Value
                : null;
        }

        private static double? ReadDouble(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetDouble(out var number))
            {
                return number;
            }

            return double.TryParse(
                property.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : null;
        }

        private static string BuildFailure(ProcessRunResult result, string command)
        {
            var builder = new StringBuilder(command);

            if (result.TimedOut)
            {
                builder.Append(" timed out.");
            }
            else if (result.Cancelled)
            {
                builder.Append(" was cancelled.");
            }
            else
            {
                builder.Append($" failed with exit code {result.ExitCode}.");
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                builder.Append(' ');
                builder.Append(result.StandardError.Trim());
            }

            return builder.ToString();
        }

        private static string RedactDownloadAuthorization(string value)
        {
            return Regex.Replace(
                value,
                @"(?i)([?&]Authorization=)[^&\s]+",
                "$1[redacted]");
        }

        private sealed record AvailabilityProbeResult(
            MediaConverterAvailability Availability,
            bool Cacheable);
    }

    public class SystemProcessRunner : IProcessRunner
    {
        public async Task<ProcessRunResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = startInfo
            };
            var stopwatch = Stopwatch.StartNew();

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

            try
            {
                await process.WaitForExitAsync(linkedSource.Token);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);

                return new ProcessRunResult(
                    process.ExitCode,
                    await stdoutTask,
                    await stderrTask,
                    stopwatch.Elapsed,
                    timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested);
            }

            return new ProcessRunResult(
                process.ExitCode,
                await stdoutTask,
                await stderrTask,
                stopwatch.Elapsed,
                false,
                false);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
