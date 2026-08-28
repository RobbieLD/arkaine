using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

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
                    _availability = await ProbeAvailabilityAsync(cancellationToken);
                }

                return _availability;
            }
            finally
            {
                _availabilityLock.Release();
            }
        }

        private async Task<MediaConverterAvailability> ProbeAvailabilityAsync(CancellationToken cancellationToken)
        {
            try
            {
                var timeout = TimeSpan.FromSeconds(_options.FFMPEG_PROBE_TIMEOUT_SECONDS);
                var versionProcess = CreateBareStartInfo();
                versionProcess.ArgumentList.Add("-version");

                var versionResult = await _runner.RunAsync(versionProcess, timeout, cancellationToken);
                if (versionResult.TimedOut || versionResult.Cancelled || versionResult.ExitCode != 0)
                {
                    return new MediaConverterAvailability(
                        false,
                        false,
                        false,
                        _options.FFMPEG_PATH,
                        string.Empty,
                        [],
                        BuildFailure(versionResult, "ffmpeg -version"));
                }

                var encodersProcess = CreateBareStartInfo();
                encodersProcess.ArgumentList.Add("-hide_banner");
                encodersProcess.ArgumentList.Add("-encoders");

                var encoderResult = await _runner.RunAsync(encodersProcess, timeout, cancellationToken);
                if (encoderResult.TimedOut || encoderResult.Cancelled || encoderResult.ExitCode != 0)
                {
                    return new MediaConverterAvailability(
                        false,
                        true,
                        false,
                        _options.FFMPEG_PATH,
                        ExtractFirstLine(versionResult.StandardOutput),
                        ["libx264", "aac"],
                        BuildFailure(encoderResult, "ffmpeg -encoders"));
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

                return new MediaConverterAvailability(
                    missingEncoders.Count == 0,
                    true,
                    missingEncoders.Count == 0,
                    _options.FFMPEG_PATH,
                    ExtractFirstLine(versionResult.StandardOutput),
                    missingEncoders,
                    string.Empty);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Win32Exception exception)
            {
                _logger.LogWarning(exception, "ffmpeg was not available at {ExecutablePath}", _options.FFMPEG_PATH);
                return new MediaConverterAvailability(false, false, false, _options.FFMPEG_PATH, string.Empty, ["libx264", "aac"], exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "ffmpeg availability probe failed.");
                return new MediaConverterAvailability(false, false, false, _options.FFMPEG_PATH, string.Empty, ["libx264", "aac"], exception.Message);
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
                result.Cancelled);
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
