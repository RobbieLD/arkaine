namespace Server.Arkaine.Ingest
{
    public interface IBackgroundTaskQueue
    {
        ValueTask EnqueueAsync(IngestRequest request, CancellationToken cancellationToken);

        ValueTask<IngestRequest> DequeueAsync(CancellationToken cancellationToken);
    }
}
