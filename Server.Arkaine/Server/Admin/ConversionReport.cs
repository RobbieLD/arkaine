namespace Server.Arkaine.Admin
{
    public sealed class ConversionReport
    {
        public int Scanned { get; set; }
        public int Converted { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Recovered { get; set; }
        public bool Finished { get; set; }
        public bool Cancelled { get; set; }
        public bool Running { get; set; }
        public string Status { get; set; } = "idle";
        public string ExactTarget { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTimeOffset? StartedUtc { get; set; }
        public DateTimeOffset? FinishedUtc { get; set; }
        public List<ConversionFailure> Failures { get; set; } = [];

        public ConversionReport Clone()
        {
            return new ConversionReport
            {
                Cancelled = Cancelled,
                Converted = Converted,
                CurrentFile = CurrentFile,
                Error = Error,
                ExactTarget = ExactTarget,
                Failed = Failed,
                Failures = Failures.Select(failure => failure with { }).ToList(),
                Finished = Finished,
                FinishedUtc = FinishedUtc,
                Recovered = Recovered,
                Running = Running,
                Scanned = Scanned,
                Skipped = Skipped,
                StartedUtc = StartedUtc,
                Status = Status
            };
        }
    }

    public sealed record ConversionFailure(string SourceFile, string TargetFile, string Error);
}
