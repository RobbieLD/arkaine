namespace Server.Arkaine.Admin
{
    public interface IProcessingReportService
    {
        Task SaveAsync(
            ProcessingReportType type,
            DateTimeOffset createdUtc,
            string html,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<ProcessingReportSummary>> ListAsync(CancellationToken cancellationToken);

        Task<StoredProcessingReport?> GetAsync(int id, CancellationToken cancellationToken);

        Task ClearAsync(CancellationToken cancellationToken);
    }
}
