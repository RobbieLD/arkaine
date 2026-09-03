using System.Text.Json.Serialization;

namespace Server.Arkaine.Admin
{
    public sealed class ConversionReport
    {
        public int Scanned { get; set; }
        public int Converted { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public bool Finished { get; set; }
        public bool Cancelled { get; set; }
        public bool Running { get; set; }
        public string Status { get; set; } = "idle";
        public string Path { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTimeOffset? StartedUtc { get; set; }
        public DateTimeOffset? FinishedUtc { get; set; }
        public List<ConversionFailure> Failures { get; set; } = [];
        [JsonIgnore]
        public List<ConversionFileResult> Files { get; set; } = [];

        public ConversionReport Clone(bool includeFiles = true)
        {
            return new ConversionReport
            {
                Cancelled = Cancelled,
                Converted = Converted,
                CurrentFile = CurrentFile,
                Error = Error,
                Failed = Failed,
                Failures = Failures.Select(failure => failure with { }).ToList(),
                Files = includeFiles
                    ? Files.Select(file => file with { }).ToList()
                    : [],
                Finished = Finished,
                FinishedUtc = FinishedUtc,
                Path = Path,
                Running = Running,
                Scanned = Scanned,
                Skipped = Skipped,
                StartedUtc = StartedUtc,
                Status = Status
            };
        }
    }

    public sealed record ConversionFailure(string SourceFile, string TargetFile, string Error);

    public sealed record ConversionFileResult(
        string SourceFile,
        string TargetFile,
        string Status,
        string FileId,
        string Type,
        string ContentType,
        string Size,
        string Details);
}
