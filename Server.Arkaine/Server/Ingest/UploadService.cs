using Microsoft.AspNetCore.SignalR;
using Server.Arkaine.B2;

namespace Server.Arkaine.Ingest
{
    public class UploadService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger _logger;
        private readonly IHubContext<IngestHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;

        public UploadService(
            IBackgroundTaskQueue taskQueue,
            IHubContext<IngestHub> hub,
            IServiceProvider serviceProvider,
            ILogger<UploadService> logger)
        {
            _taskQueue = taskQueue;
            _hubContext = hub;
            _serviceProvider = serviceProvider;
            _logger = logger;
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

                using var scope = _serviceProvider.CreateScope();
                var uploader = scope.ServiceProvider.GetRequiredService<IB2Service>();

                try
                {
                    await uploader.Upload(request, cancellationToken);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex.Message);
                    _logger.LogError(ex.StackTrace);
                    await _hubContext.Clients.All.SendAsync("update", $"Error processing upload: {ex.Message}", cancellationToken);
                }                
            }
        }
    }
}
