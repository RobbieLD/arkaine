using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Server.Arkaine.Admin
{
    [Index(nameof(FileName), nameof(FileId), IsUnique = true)]
    [Index(nameof(Status))]
    public sealed class VideoConversionRequest
    {
        public int Id { get; set; }

        [MaxLength(1024)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string FileId { get; set; } = string.Empty;

        [MaxLength(256)]
        public string RequestedBy { get; set; } = string.Empty;

        [MaxLength(32)]
        public string Status { get; set; } = VideoConversionRequestStatus.Queued;

        [MaxLength(32)]
        public string Reason { get; set; } = VideoConversionRequestReason.Manual;

        [MaxLength(2000)]
        public string Error { get; set; } = string.Empty;

        public DateTimeOffset RequestedUtc { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }
    }

    public static class VideoConversionRequestStatus
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }

    public static class VideoConversionRequestReason
    {
        public const string Manual = "manual";
        public const string Automatic = "automatic";
    }

    public sealed record VideoConversionRequestResponse(
        int Id,
        string FileName,
        string FileId,
        string RequestedBy,
        string Status,
        string Reason,
        string Error,
        DateTimeOffset RequestedUtc,
        DateTimeOffset UpdatedUtc);

    public sealed class VideoConversionRequestInput
    {
        public string FileName { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
    }
}
