using Microsoft.AspNetCore.SignalR;
using Server.Arkaine.B2;

namespace Server.Arkaine.Ingest
{
    public class UploadService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IB2Service _uploader;
        private readonly IExtractorFactory _extractorFactory;
        private readonly IHubContext<IngestHub> _hubContext;

        public UploadService(
            IBackgroundTaskQueue taskQueue,
            IB2Service uploader,
            IExtractorFactory extractorFactory,
            IHubContext<IngestHub> hub)
        {
            _taskQueue = taskQueue;
            _uploader = uploader;
            _extractorFactory = extractorFactory;
            _hubContext = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ProcessUploadQueue(stoppingToken);
        }

        private async Task ProcessUploadQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = await _taskQueue.DequeueAsync(cancellationToken);

                var extractor = _extractorFactory.GetExtractor(request.Url);
                var resp = await extractor.Extract(request.Url, request.Name, cancellationToken);

                try
                {
                    await _uploader.Upload(resp.FileName, resp.MimeType, resp.Content, cancellationToken);
                }
                catch(Exception ex)
                {
                    await _hubContext.Clients.All.SendAsync("update", $"Error processing upload: {ex.Message}", cancellationToken);
                }
                
            }
        }
    }
}
