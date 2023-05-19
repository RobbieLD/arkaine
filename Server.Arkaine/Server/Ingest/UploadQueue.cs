using System.Threading.Channels;

namespace Server.Arkaine.Ingest
{
    public class UploadQueue : IBackgroundTaskQueue
    {
        private readonly Channel<IngestRequest> _queue;

        public UploadQueue()
        {
            BoundedChannelOptions options = new(5)
            {
                FullMode = BoundedChannelFullMode.Wait
            };

            _queue = Channel.CreateBounded<IngestRequest>(options);
        }

        public async ValueTask<IngestRequest> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }

        public async ValueTask EnqueueAsync(IngestRequest request, CancellationToken cancellationToken)
        {
            await _queue.Writer.WriteAsync(request, cancellationToken);
        }
    }
}
