namespace Server.Arkaine.Admin
{
    public interface IVideoConversionRequestStore
    {
        Task<VideoConversionRequest> EnqueueAsync(
            string fileName,
            string fileId,
            string requestedBy,
            string reason,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<VideoConversionRequest>> GetPendingAsync(
            string? prefix,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<VideoConversionRequestResponse>> ListAsync(
            string? status,
            CancellationToken cancellationToken);

        Task MarkRunningAsync(int id, CancellationToken cancellationToken);
        Task MarkCompletedAsync(int id, CancellationToken cancellationToken);
        Task MarkFailedAsync(int id, string error, CancellationToken cancellationToken);
        Task MarkQueuedAsync(int id, CancellationToken cancellationToken);
        Task<bool> CancelAsync(int id, CancellationToken cancellationToken);
    }
}
