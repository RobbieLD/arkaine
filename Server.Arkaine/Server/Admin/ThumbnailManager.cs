using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using Server.Arkaine.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace Server.Arkaine.Admin
{
    public class ThumbnailManager
    {
        private readonly object _syncRoot = new();
        private readonly ArkaineOptions _options;
        private readonly ILogger<ThumbnailManager> _logger;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly AdminJobCoordinator _jobCoordinator;
        private readonly IMediaConverter _converter;

        private CancellationTokenSource? _stoppingToken;
        private Task? _runningTask;
        private GenerationReport _report = new();

        public ThumbnailManager(
            IServiceProvider serviceProvider,
            IOptions<ArkaineOptions> config,
            IHubContext<AdminHub> hubContext,
            AdminJobCoordinator jobCoordinator,
            IMediaConverter converter,
            ILogger<ThumbnailManager> logger)
        {
            _options = config.Value;
            _options.Normalize();
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _jobCoordinator = jobCoordinator;
            _converter = converter;
        }

        public bool TryStart(string userName)
        {
            lock (_syncRoot)
            {
                if (IsRunning)
                {
                    return false;
                }

                if (!_jobCoordinator.TryAcquire(AdminJobKind.Thumbnails))
                {
                    return false;
                }

                try
                {
                    _stoppingToken?.Dispose();
                    _stoppingToken = new CancellationTokenSource();
                    _report = new GenerationReport
                    {
                        Running = true,
                        StartedUtc = DateTimeOffset.UtcNow,
                        Status = "running"
                    };
                    _runningTask = RunAsync(userName, _stoppingToken.Token);
                    ObserveTask(_runningTask);
                    return true;
                }
                catch
                {
                    _jobCoordinator.Release(AdminJobKind.Thumbnails);
                    throw;
                }
            }
        }

        public void CancelGeneration()
        {
            lock (_syncRoot)
            {
                if (_stoppingToken is null)
                {
                    return;
                }

                if (_report.Status == "running")
                {
                    _report.Status = "cancelling";
                }

                _stoppingToken.Cancel();
            }
        }

        public SettingsResponse GetSettings()
        {
            var status = GetStatus();
            return new SettingsResponse(
                status.TotalThumbnails,
                status.ThumbnailPageSize,
                status.ThumbnailWidth,
                status.ThumbnailDir,
                status.ThumbnailExtensions,
                status.IsRunning);
        }

        public ThumbnailJobStatusResponse GetStatus()
        {
            return new ThumbnailJobStatusResponse(
                CountThumbnails(),
                _options.THUMBNAIL_PAGE_SIZE,
                _options.THUMBNAIL_WIDTH,
                _options.THUMBNAIL_DIR,
                _options.THUMBNAIL_EXTENSIONS,
                IsRunning,
                SnapshotReport());
        }

        public async Task GenerateThumbnails(string userName)
        {
            if (!TryStart(userName))
            {
                return;
            }

            var task = GetCurrentTask();
            if (task is not null)
            {
                await task;
            }
        }

        public async Task WaitForCompletionAsync()
        {
            var task = GetCurrentTask();
            if (task is not null)
            {
                await task;
            }
        }

        private bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _runningTask is { IsCompleted: false };
                }
            }
        }

        private Task? GetCurrentTask()
        {
            lock (_syncRoot)
            {
                return _runningTask;
            }
        }

        private async Task RunAsync(string userName, CancellationToken cancellationToken)
        {
            try
            {
                var request = new FilesRequest
                {
                    BucketId = _options.BUCKET_ID,
                    PageSize = _options.THUMBNAIL_PAGE_SIZE
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var uploader = scope.ServiceProvider.GetRequiredService<IB2Service>();
                    var page = await uploader.ListFiles(request, userName, null, cancellationToken);

                    await ProcessPage(page, userName, uploader, cancellationToken);
                    request.StartFile = page.NextFileName;

                    if (string.IsNullOrEmpty(page.NextFileName))
                    {
                        break;
                    }
                }

                lock (_syncRoot)
                {
                    _report.Status = cancellationToken.IsCancellationRequested ? "cancelled" : "completed";
                    _report.Cancelled = cancellationToken.IsCancellationRequested;
                }
            }
            catch (OperationCanceledException)
            {
                lock (_syncRoot)
                {
                    _report.Status = "cancelled";
                    _report.Cancelled = true;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Thumbnail generation failed.");
                lock (_syncRoot)
                {
                    _report.Error = exception.Message;
                    _report.Status = "failed";
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    _report.Running = false;
                    _report.Finished = true;
                    _report.FinishedUtc = DateTimeOffset.UtcNow;
                }

                await SaveReportAsync(SnapshotReport());
                _jobCoordinator.Release(AdminJobKind.Thumbnails);
                await _hubContext.Clients.All.SendAsync("update", SnapshotReport());
            }
        }

        private async Task ProcessPage(FilesResponse page, string userName, IB2Service uploader, CancellationToken cancellationToken)
        {
            foreach (var file in page.Files)
            {
                lock (_syncRoot)
                {
                    _report.Scanned++;
                    _report.CurrentFile = file.FileName;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
                if (!isVideo && !_options.IsThumbnailExtension(file.FileName))
                {
                    continue;
                }

                var relativeThumbnailPath = ThumbnailPreview.GetRelativeThumbnailPath(file);
                if (!ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, relativeThumbnailPath, out var fullName))
                {
                    _logger.LogWarning("Skipping thumbnail with an unsafe file name: {FileName}", file.FileName);
                    RecordFailure(file, "The thumbnail path is unsafe.");
                    continue;
                }

                if (File.Exists(fullName))
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullName) ?? throw new InvalidOperationException($"{file.FileName} is not a valid file name."));
                    if (isVideo)
                    {
                        await GenerateVideoThumbnail(userName, fullName, file.FileName, uploader, cancellationToken);
                    }
                    else
                    {
                        await GenerateImageThumbnail(userName, fullName, file.FileName, uploader, cancellationToken);
                    }
                    lock (_syncRoot)
                    {
                        _report.Generated++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Generating thumbnail failed for {FileName}", file.FileName);
                    RecordFailure(file, exception.Message);
                }

                if (SnapshotReport().Scanned % 100 == 0)
                {
                    await _hubContext.Clients.All.SendAsync("update", SnapshotReport());
                }
            }
        }

        private async Task GenerateImageThumbnail(
            string userName,
            string thumbnailName,
            string fileName,
            IB2Service uploader,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Generating thumbnail {ThumbnailName}", thumbnailName);
            await using var stream = await uploader.Download(userName, fileName, cancellationToken);
            using var image = Image.Load(stream);
            image.Mutate(context => context.Resize(_options.THUMBNAIL_WIDTH, 0));
            await image.SaveAsJpegAsync(thumbnailName, cancellationToken);
        }

        private async Task GenerateVideoThumbnail(
            string userName,
            string thumbnailName,
            string fileName,
            IB2Service uploader,
            CancellationToken cancellationToken)
        {
            var tempDirectory = Path.Combine(_options.GetConversionTempDirectory(), Guid.NewGuid().ToString("n"));
            var tempTarget = Path.Combine(tempDirectory, "frame.jpg");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                _logger.LogInformation("Generating video thumbnail {ThumbnailName}", thumbnailName);
                var sourceUrl = await uploader.GetDownloadUrl(userName, fileName, cancellationToken);
                var result = await ExtractVideoFrame(
                    sourceUrl,
                    tempTarget,
                    cancellationToken);

                if (result.HttpStatusCode == 401)
                {
                    _logger.LogWarning("Video thumbnail source authorization expired for {FileName}; retrying.", fileName);
                    sourceUrl = await uploader.GetDownloadUrl(userName, fileName, cancellationToken);
                    result = await ExtractVideoFrame(sourceUrl, tempTarget, cancellationToken);
                }

                if (result.Cancelled || cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (!result.Success)
                {
                    var error = string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"ffmpeg exited with code {result.ExitCode}."
                        : RedactDownloadAuthorization(result.StandardError);
                    throw result.TimedOut
                        ? new TimeoutException(error)
                        : new InvalidOperationException(error);
                }

                var frameInfo = new FileInfo(tempTarget);
                if (!frameInfo.Exists || frameInfo.Length == 0)
                {
                    throw new InvalidOperationException("ffmpeg did not produce a video thumbnail.");
                }

                using var image = Image.Load(tempTarget);
                image.Mutate(context => context.Resize(_options.THUMBNAIL_WIDTH, 0));
                await image.SaveAsJpegAsync(thumbnailName, cancellationToken);
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private Task<MediaConversionResult> ExtractVideoFrame(
            Uri sourceUrl,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _converter.ConvertAsync(
                new MediaConversionRequest(
                    sourceUrl.AbsoluteUri,
                    targetPath,
                    MediaConversionKind.Image,
                    TimeSpan.FromSeconds(_options.CONVERT_VIDEO_TIMEOUT_SECONDS)),
                cancellationToken);
        }

        private static string RedactDownloadAuthorization(string error)
        {
            return Regex.Replace(
                error,
                @"(?i)([?&]Authorization=)[^&\s]+",
                "$1[redacted]");
        }

        private long CountThumbnails()
        {
            return EnumerateManagedFiles()
                .Count(file => _options.GetThumbnailExtensions().Contains(Path.GetExtension(file)));
        }

        private IEnumerable<string> EnumerateManagedFiles()
        {
            if (string.IsNullOrWhiteSpace(_options.THUMBNAIL_DIR) || !Directory.Exists(_options.THUMBNAIL_DIR))
            {
                return [];
            }

            return Directory.EnumerateFiles(_options.THUMBNAIL_DIR, "*", SearchOption.AllDirectories)
                .Where(file => !IsInternalPath(file));
        }

        private bool IsInternalPath(string file)
        {
            var relative = Path.GetRelativePath(_options.THUMBNAIL_DIR, file)
                .Replace('\\', '/');
            return relative.StartsWith(".conversion-temp/", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private GenerationReport SnapshotReport()
        {
            lock (_syncRoot)
            {
                return _report.Clone();
            }
        }

        private void RecordFailure(B2File file, string error)
        {
            lock (_syncRoot)
            {
                _report.Failed++;
                _report.Failures.Add(new ThumbnailFailure(
                    file.FileName,
                    file.Id,
                    file.Type,
                    file.ContentType,
                    file.Size,
                    error));
            }
        }

        private async Task SaveReportAsync(GenerationReport report)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reports = scope.ServiceProvider.GetRequiredService<IProcessingReportService>();
                await reports.SaveAsync(
                    ProcessingReportType.Thumbnail,
                    report.FinishedUtc ?? DateTimeOffset.UtcNow,
                    ProcessingReportHtmlRenderer.RenderThumbnail(report),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not save the thumbnail generation report.");
                lock (_syncRoot)
                {
                    _report.Error = string.IsNullOrWhiteSpace(_report.Error)
                        ? $"Could not save the report: {exception.Message}"
                        : $"{_report.Error} Could not save the report: {exception.Message}";
                }
            }
        }

        private void ObserveTask(Task task)
        {
            _ = task.ContinueWith(
                completedTask =>
                {
                    if (completedTask.Exception is not null)
                    {
                        _logger.LogError(completedTask.Exception, "Thumbnail background task failed.");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
