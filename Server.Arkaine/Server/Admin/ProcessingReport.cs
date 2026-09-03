using System.ComponentModel.DataAnnotations;

namespace Server.Arkaine.Admin
{
    public sealed class ProcessingReport
    {
        public int Id { get; set; }

        [MaxLength(32)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public string Html { get; set; } = string.Empty;
    }

    public enum ProcessingReportType
    {
        Thumbnail,
        Conversion
    }

    public sealed record ProcessingReportSummary(
        int Id,
        string Type,
        string Name,
        DateTimeOffset CreatedUtc);

    public sealed record StoredProcessingReport(
        int Id,
        string Type,
        string Name,
        DateTimeOffset CreatedUtc,
        string Html);
}
