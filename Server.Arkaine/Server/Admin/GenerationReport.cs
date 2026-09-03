namespace Server.Arkaine.Admin
{
    public class GenerationReport
    {
        public int Generated { get; set; }
        public int Failed { get; set; }
        public int Scanned { get; set; }
        public bool Finished { get; set; }
        public bool Cancelled { get; set; }
        public bool Running { get; set; }
        public string Status { get; set; } = "idle";
        public string CurrentFile { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTimeOffset? StartedUtc { get; set; }
        public DateTimeOffset? FinishedUtc { get; set; }
        public List<ThumbnailFailure> Failures { get; set; } = [];

        public GenerationReport Clone()
        {
            return new GenerationReport
            {
                Cancelled = Cancelled,
                CurrentFile = CurrentFile,
                Error = Error,
                Failed = Failed,
                Finished = Finished,
                FinishedUtc = FinishedUtc,
                Failures = Failures.Select(failure => failure with { }).ToList(),
                Generated = Generated,
                Running = Running,
                Scanned = Scanned,
                StartedUtc = StartedUtc,
                Status = Status
            };
        }
    }

    public sealed record ThumbnailFailure(
        string FileName,
        string FileId,
        string Type,
        string ContentType,
        string Size,
        string Error);
}
