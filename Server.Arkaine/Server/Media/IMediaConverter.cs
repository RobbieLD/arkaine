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
        TimeSpan Timeout);

    public sealed record MediaConversionResult(
        bool Success,
        int ExitCode,
        string StandardError,
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
        Task<MediaConversionResult> ConvertAsync(MediaConversionRequest request, CancellationToken cancellationToken);
    }

    public interface IProcessRunner
    {
        Task<ProcessRunResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
