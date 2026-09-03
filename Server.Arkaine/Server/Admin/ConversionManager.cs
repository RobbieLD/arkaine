using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using Server.Arkaine.Media;

namespace Server.Arkaine.Admin
{
    public class ConversionManager
    {
        private readonly object _syncRoot = new();
        private readonly ArkaineOptions _options;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly IMediaConverter _converter;
        private readonly AdminJobCoordinator _jobCoordinator;
        private readonly ILogger<ConversionManager> _logger;

        private CancellationTokenSource? _stoppingToken;
        private Task? _runningTask;
        private ConversionReport _report = new();

        public ConversionManager(
            IServiceProvider serviceProvider,
            IOptions<ArkaineOptions> options,
            IHubContext<AdminHub> hubContext,
            IMediaConverter converter,
            AdminJobCoordinator jobCoordinator,
            ILogger<ConversionManager> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _options.Normalize();
            _hubContext = hubContext;
            _converter = converter;
            _jobCoordinator = jobCoordinator;
            _logger = logger;
        }

        public bool TryStart(string userName, string? path)
        {
            var normalizedPath = ConversionPath.Normalize(path);

            lock (_syncRoot)
            {
                if (IsRunning)
                {
                    return false;
                }

                if (!_jobCoordinator.TryAcquire(AdminJobKind.Conversion))
                {
                    return false;
                }

                try
                {
                    _stoppingToken?.Dispose();
                    _stoppingToken = new CancellationTokenSource();
                    _report = new ConversionReport
                    {
                        Path = normalizedPath,
                        Running = true,
                        StartedUtc = DateTimeOffset.UtcNow,
                        Status = "running"
                    };
                    _runningTask = RunAsync(userName, normalizedPath, _stoppingToken.Token);
                    ObserveTask(_runningTask);
                    return true;
                }
                catch
                {
                    _jobCoordinator.Release(AdminJobKind.Conversion);
                    throw;
                }
            }
        }

        public void Cancel()
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

        public async Task ConvertAsync(string userName, string? path)
        {
            if (!TryStart(userName, path))
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

        public ConversionJobStatusResponse GetStatus()
        {
            return new ConversionJobStatusResponse(
                _options.CONVERT_PAGE_SIZE,
                _options.CONVERT_IMAGE_EXTENSIONS,
                _options.CONVERT_VIDEO_EXTENSIONS,
                _options.GetConversionTempDirectory(),
                IsRunning,
                SnapshotReport());
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

        private async Task RunAsync(
            string userName,
            string path,
            CancellationToken cancellationToken)
        {
            try
            {
                var availability = await _converter.GetAvailabilityAsync(cancellationToken);
                if (!availability.IsAvailable)
                {
                    lock (_syncRoot)
                    {
                        _report.Error = string.IsNullOrWhiteSpace(availability.Error)
                            ? "ffmpeg is not available."
                            : availability.Error;
                        _report.Status = "failed";
                    }

                    return;
                }

                var request = new FilesRequest
                {
                    BucketId = _options.BUCKET_ID,
                    PageSize = _options.CONVERT_PAGE_SIZE,
                    Prefix = path
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var b2 = scope.ServiceProvider.GetRequiredService<IB2Service>();
                    var page = await b2.ListFiles(request, userName, null, cancellationToken);

                    await ProcessPageAsync(page, userName, b2, cancellationToken);
                    request.StartFile = page.NextFileName;

                    if (string.IsNullOrEmpty(page.NextFileName))
                    {
                        break;
                    }
                }

                lock (_syncRoot)
                {
                    if (_report.Status is "running" or "cancelling")
                    {
                        _report.Status = cancellationToken.IsCancellationRequested ? "cancelled" : "completed";
                        _report.Cancelled = cancellationToken.IsCancellationRequested;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_syncRoot)
                {
                    _report.Cancelled = true;
                    _report.Status = "cancelled";
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Media conversion failed.");
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

                await SaveReportAsync(SnapshotReport(includeFiles: true));
                _jobCoordinator.Release(AdminJobKind.Conversion);
                await _hubContext.Clients.All.SendAsync("convert", SnapshotReport());
            }
        }

        private async Task ProcessPageAsync(
            FilesResponse page,
            string userName,
            IB2Service b2,
            CancellationToken cancellationToken)
        {
            foreach (var file in page.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_syncRoot)
                {
                    _report.Scanned++;
                    _report.CurrentFile = file.FileName;
                }

                if (!_options.IsConvertibleImage(file.FileName) &&
                    !_options.IsConvertibleVideo(file.FileName))
                {
                    RecordSkipped(file, string.Empty, "The file type is already supported.");
                    continue;
                }

                var targetFile = _options.GetConversionTargetFileName(file.FileName);

                try
                {
                    if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is not null)
                    {
                        RecordSkipped(file, targetFile, "The destination file already exists.");
                        continue;
                    }

                    await ConvertAndUploadAsync(file, targetFile, userName, b2, cancellationToken);
                    lock (_syncRoot)
                    {
                        _report.Converted++;
                        _report.Files.Add(new ConversionFileResult(
                            file.FileName,
                            targetFile,
                            "converted",
                            file.Id,
                            file.Type,
                            file.ContentType,
                            file.Size,
                            string.Empty));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    RecordFailure(file, targetFile, exception);
                    await _hubContext.Clients.All.SendAsync("convert", SnapshotReport(), cancellationToken);
                }

                if (SnapshotReport().Scanned % 25 == 0 || SnapshotReport().Converted > 0)
                {
                    await _hubContext.Clients.All.SendAsync("convert", SnapshotReport(), cancellationToken);
                }
            }
        }

        private async Task ConvertAndUploadAsync(
            B2File file,
            string targetFile,
            string userName,
            IB2Service b2,
            CancellationToken cancellationToken)
        {
            var kind = _options.IsConvertibleImage(file.FileName)
                ? MediaConversionKind.Image
                : MediaConversionKind.Video;
            var tempDirectory = Path.Combine(_options.GetConversionTempDirectory(), Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(tempDirectory);

            var sourceExtension = Path.GetExtension(file.FileName);
            var targetExtension = kind == MediaConversionKind.Image ? ".jpg" : ".mp4";
            var tempSource = Path.Combine(tempDirectory, $"source{sourceExtension}");
            var tempTarget = Path.Combine(tempDirectory, $"target{targetExtension}");

            try
            {
                await using (var remoteStream = await b2.Download(userName, file.FileName, cancellationToken))
                await using (var output = File.Create(tempSource))
                {
                    await remoteStream.CopyToAsync(output, cancellationToken);
                }

                var result = await _converter.ConvertAsync(
                    new MediaConversionRequest(
                        tempSource,
                        tempTarget,
                        kind,
                        TimeSpan.FromSeconds(kind == MediaConversionKind.Image
                            ? _options.CONVERT_IMAGE_TIMEOUT_SECONDS
                            : _options.CONVERT_VIDEO_TIMEOUT_SECONDS)),
                    cancellationToken);

                if (!result.Success)
                {
                    if (result.Cancelled || cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    var error = string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"ffmpeg exited with code {result.ExitCode}."
                        : result.StandardError;

                    throw result.TimedOut
                        ? new TimeoutException(error)
                        : new InvalidOperationException(error);
                }

                var fileInfo = new FileInfo(tempTarget);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("ffmpeg did not produce an output file.");
                }

                await using var convertedStream = File.OpenRead(tempTarget);
                var contentType = kind == MediaConversionKind.Image ? "image/jpeg" : "video/mp4";

                if (fileInfo.Length > _options.UPLOAD_CHUNK_SIZE)
                {
                    await b2.UploadMultiPartFile(
                        targetFile,
                        contentType,
                        convertedStream,
                        _options.UPLOAD_CHUNK_SIZE,
                        cancellationToken);
                }
                else
                {
                    await b2.UploadSingleFile(
                        targetFile,
                        contentType,
                        fileInfo.Length,
                        convertedStream,
                        cancellationToken);
                }

                if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is null)
                {
                    throw new InvalidOperationException(
                        $"Uploaded conversion target '{targetFile}' could not be verified.");
                }
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private async Task<B2File?> GetExactFileAsync(
            IB2Service b2,
            string userName,
            string fileName,
            CancellationToken cancellationToken)
        {
            var response = await b2.ListFiles(new FilesRequest
            {
                BucketId = _options.BUCKET_ID,
                ExactFileName = fileName,
                PageSize = 1
            }, userName, null, cancellationToken);

            return response.Files.SingleOrDefault();
        }

        private void RecordSkipped(B2File file, string targetFile, string details)
        {
            lock (_syncRoot)
            {
                _report.Skipped++;
                _report.Files.Add(new ConversionFileResult(
                    file.FileName,
                    targetFile,
                    "skipped",
                    file.Id,
                    file.Type,
                    file.ContentType,
                    file.Size,
                    details));
            }
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

        private ConversionReport SnapshotReport(bool includeFiles = false)
        {
            lock (_syncRoot)
            {
                return _report.Clone(includeFiles);
            }
        }

        private void RecordFailure(B2File file, string targetFile, Exception exception)
        {
            _logger.LogError(exception, "Conversion failed for {SourceFile}", file.FileName);
            lock (_syncRoot)
            {
                _report.Failed++;
                _report.Status = "running";
                _report.Failures.Add(new ConversionFailure(file.FileName, targetFile, exception.Message));
                _report.Files.Add(new ConversionFileResult(
                    file.FileName,
                    targetFile,
                    "error",
                    file.Id,
                    file.Type,
                    file.ContentType,
                    file.Size,
                    exception.Message));
            }
        }

        private async Task SaveReportAsync(ConversionReport report)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reports = scope.ServiceProvider.GetRequiredService<IProcessingReportService>();
                await reports.SaveAsync(
                    ProcessingReportType.Conversion,
                    report.FinishedUtc ?? DateTimeOffset.UtcNow,
                    ProcessingReportHtmlRenderer.RenderConversion(report),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not save the media conversion report.");
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
                        _logger.LogError(completedTask.Exception, "Conversion background task failed.");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
