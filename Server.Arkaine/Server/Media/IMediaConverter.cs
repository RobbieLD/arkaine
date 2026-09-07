using System.Diagnostics;

namespace Server.Arkaine.Media
{
    public enum MediaConversionKind
    {
        Image,
        Video
    }

    public sealed record MediaConversionRequest(
        string SourcePath,
        string TargetPath,
        MediaConversionKind Kind,
        TimeSpan Timeout,
        TimeSpan? Duration = null,
        Func<MediaConversionProgress, ValueTask>? Progress = null);

    public sealed record MediaConversionProgress(
        long? Frame,
        TimeSpan? OutputTime,
        double? Speed,
        long? TotalSize,
        double? Percent,
        bool Completed);

    public sealed record MediaConversionResult(
        bool Success,
        int ExitCode,
        string StandardError,
        TimeSpan Duration,
        bool TimedOut,
        bool Cancelled,
        int? HttpStatusCode = null);

    public sealed record MediaMetadata(
        TimeSpan? Duration,
        long? FormatBitrate,
        long? VideoBitrate,
        string VideoCodec,
        int? Width,
        int? Height,
        double? FrameRate,
        long? AudioBitrate,
        string AudioCodec,
        long? FileSize = null);

    public sealed record MediaMetadataResult(
        bool Success,
        MediaMetadata? Metadata,
        string Error,
        TimeSpan Duration,
        bool TimedOut,
        bool Cancelled,
        int? HttpStatusCode = null);

    public sealed record MediaConverterAvailability(
        bool IsAvailable,
        bool SupportsImageConversion,
        bool SupportsVideoConversion,
        string ExecutablePath,
        string Version,
        IReadOnlyList<string> MissingEncoders,
        string Error);

    public sealed record ProcessRunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        TimeSpan Duration,
        bool TimedOut,
        bool Cancelled);

    public interface IMediaConverter
    {
        Task<MediaConverterAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);
        Task<MediaMetadataResult> ProbeAsync(string sourcePath, CancellationToken cancellationToken);
        Task<MediaConversionResult> ConvertAsync(MediaConversionRequest request, CancellationToken cancellationToken);
    }

    public interface IProcessRunner
    {
        Task<ProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Func<string, ValueTask>? onStandardOutputLine = null);
    }
}
