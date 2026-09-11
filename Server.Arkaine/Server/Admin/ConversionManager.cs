using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using Server.Arkaine.Media;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Server.Arkaine.Admin
{
    public class ConversionManager
    {
        private const int ProgressNotificationScanInterval = 25;
        private static readonly TimeSpan ProgressNotificationMinimumInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan VideoDurationTolerance = TimeSpan.FromMilliseconds(500);

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
            return TryStart(userName, path, null);
        }

        public bool TryStartForFile(string userName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A file name must be supplied.", nameof(fileName));
            }

            return TryStart(userName, ConversionPath.ForFileName(fileName), fileName);
        }

        private bool TryStart(string userName, string? path, string? exactFileName)
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
                    _runningTask = RunAsync(userName, normalizedPath, exactFileName, _stoppingToken.Token);
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
                _options.CONVERT_VIDEO_MAX_BITRATE,
                _options.GetConversionTempDirectory(),
                IsRunning,
                SnapshotReport());
        }

        public bool IsRunning
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
            string? exactFileName,
            CancellationToken cancellationToken)
        {
            var progressNotification = new ProgressNotificationState();

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
                    PageSize = exactFileName is null ? _options.CONVERT_PAGE_SIZE : 1,
                    Prefix = exactFileName is null ? path : null,
                    ExactFileName = exactFileName
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var b2 = scope.ServiceProvider.GetRequiredService<IB2Service>();
                    var page = await b2.ListFiles(request, userName, null, cancellationToken);

                    var pendingRequests = await GetPendingRequestsAsync(path, cancellationToken);
                    if (exactFileName is not null && page.Files.Count == 0)
                    {
                        await MarkMissingRequestAsync(exactFileName, pendingRequests);
                        break;
                    }

                    await ProcessPageAsync(
                        page,
                        userName,
                        b2,
                        pendingRequests,
                        progressNotification,
                        cancellationToken);
                    await PublishProgressAsync(progressNotification, force: true);
                    request.StartFile = page.NextFileName;

                    if (exactFileName is not null || string.IsNullOrEmpty(page.NextFileName))
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

                await SetCurrentFilePhaseAsync(
                    progressNotification,
                    "cancelled",
                    force: true,
                    clearPercent: true);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Media conversion failed.");
                lock (_syncRoot)
                {
                    _report.Error = exception.Message;
                    _report.Status = "failed";
                }

                await SetCurrentFilePhaseAsync(
                    progressNotification,
                    "failed",
                    force: true,
                    clearPercent: true);
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
                await PublishProgressAsync(progressNotification, force: true);
            }
        }

        private async Task ProcessPageAsync(
            FilesResponse page,
            string userName,
            IB2Service b2,
            IReadOnlyList<VideoConversionRequest> pendingRequests,
            ProgressNotificationState progressNotification,
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

                BeginCurrentFileProgress(progressNotification);
                await PublishProgressAsync(progressNotification);

                if (_options.IsCompressedVideo(file.FileName))
                {
                    await RecordSkippedAsync(
                        file,
                        string.Empty,
                        "Compressed video outputs are not converted again.",
                        progressNotification);
                    continue;
                }

                var isImage = _options.IsConvertibleImage(file.FileName);
                var isConfiguredVideo = _options.IsConvertibleVideo(file.FileName);
                var isVideo = _options.IsVideoFile(file.FileName, file.ContentType);
                var pendingRequest = FindPendingRequest(pendingRequests, file);

                if (!isImage && !isConfiguredVideo && !isVideo)
                {
                    await RecordSkippedAsync(
                        file,
                        string.Empty,
                        "The file type is already supported.",
                        progressNotification);
                    continue;
                }

                var targetFile = isImage
                    ? _options.GetConversionTargetFileName(file.FileName)
                    : _options.GetCompressedVideoTargetFileName(file.FileName);
                var skipQueuedRequest = false;
                long? sourceSize = null;

                if (!isImage && isVideo && !isConfiguredVideo)
                {
                    await SetCurrentFilePhaseAsync(progressNotification, "probing", force: true);
                    var videoDecision = await GetVideoConversionDecisionAsync(
                        file,
                        userName,
                        b2,
                        cancellationToken);
                    sourceSize = videoDecision.SourceSize;
                    progressNotification.CurrentFileDuration = videoDecision.Duration;
                    await SetCurrentFilePhaseAsync(progressNotification, "preparing");

                    if (pendingRequest is null && !videoDecision.ShouldConvert)
                    {
                        await RecordSkippedAsync(
                            file,
                            string.Empty,
                            videoDecision.Details,
                            progressNotification);
                        continue;
                    }

                    if (pendingRequest is null)
                    {
                        pendingRequest = await EnqueueAutomaticRequestAsync(
                            file,
                            userName,
                            cancellationToken);
                    }
                    else if (videoDecision.HasKnownBitrate && !videoDecision.ShouldConvert)
                    {
                        skipQueuedRequest = true;
                    }
                }

                var conversionAttempt = new ConversionAttemptDetails();

                try
                {
                    if (skipQueuedRequest)
                    {
                        await RecordSkippedAsync(
                            file,
                            targetFile,
                            "The video is already within the configured bitrate limit; compression was skipped.",
                            progressNotification);
                        await MarkCompletedAsync(pendingRequest?.Id, cancellationToken);
                        continue;
                    }

                    if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is not null)
                    {
                        await RecordSkippedAsync(
                            file,
                            targetFile,
                            "The destination file already exists.",
                            progressNotification);
                        await MarkCompletedAsync(pendingRequest?.Id, cancellationToken);
                        continue;
                    }

                    await MarkRunningAsync(pendingRequest?.Id, cancellationToken);
                    var uploaded = await ConvertAndUploadAsync(
                        file,
                        targetFile,
                        userName,
                        b2,
                        sourceSize,
                        progressNotification.CurrentFileDuration,
                        conversionAttempt,
                        progressNotification,
                        cancellationToken);
                    await MarkCompletedAsync(pendingRequest?.Id, cancellationToken);
                    if (!uploaded)
                    {
                        await RecordSkippedAsync(
                            file,
                            targetFile,
                            "The converted video was not smaller than the source; no output was uploaded.",
                            progressNotification,
                            conversionAttempt.ConvertedSize,
                            conversionAttempt.Command);
                        continue;
                    }

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
                            string.Empty,
                            conversionAttempt.ConvertedSize,
                            conversionAttempt.Command));
                    }

                    await SetCurrentFilePhaseAsync(
                        progressNotification,
                        "completed",
                        percent: 100);
                }
                catch (OperationCanceledException)
                {
                    await MarkQueuedAsync(pendingRequest?.Id, CancellationToken.None);
                    await SetCurrentFilePhaseAsync(
                        progressNotification,
                        "cancelled",
                        force: true,
                        clearPercent: true);
                    throw;
                }
                catch (Exception exception)
                {
                    await MarkFailedAsync(pendingRequest?.Id, RedactDownloadAuthorization(exception.Message), CancellationToken.None);
                    RecordFailure(file, targetFile, exception, conversionAttempt);
                    await SetCurrentFilePhaseAsync(
                        progressNotification,
                        "failed",
                        force: true,
                        clearPercent: true);
                }
            }
        }

        private async Task<bool> ConvertAndUploadAsync(
            B2File file,
            string targetFile,
            string userName,
            IB2Service b2,
            long? sourceSize,
            TimeSpan? sourceDuration,
            ConversionAttemptDetails conversionAttempt,
            ProgressNotificationState progressNotification,
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
                var sourcePath = tempSource;
                var originalDuration = sourceDuration;
                if (kind == MediaConversionKind.Video)
                {
                    await SetCurrentFilePhaseAsync(progressNotification, "downloading", force: true);
                    await using (var remoteStream = await b2.Download(userName, file.FileName, cancellationToken))
                    await using (var output = File.Create(tempSource))
                    {
                        await remoteStream.CopyToAsync(output, cancellationToken);
                    }

                    sourcePath = tempSource;
                    var sourceInfo = new FileInfo(tempSource);
                    if (sourceSize is null or <= 0)
                    {
                        sourceSize = sourceInfo.Length;
                    }

                    if (originalDuration is null)
                    {
                        await SetCurrentFilePhaseAsync(progressNotification, "probing", force: true);
                        var sourceMetadata = await _converter.ProbeAsync(sourcePath, cancellationToken);

                        if (sourceMetadata.Success && sourceMetadata.Metadata?.Duration is { } duration)
                        {
                            originalDuration = duration;
                            progressNotification.CurrentFileDuration = duration;
                        }
                    }

                    await SetCurrentFilePhaseAsync(progressNotification, "encoding", force: true);
                }
                else
                {
                    await SetCurrentFilePhaseAsync(progressNotification, "downloading", force: true);
                    await using var remoteStream = await b2.Download(userName, file.FileName, cancellationToken);
                    await using var output = File.Create(tempSource);
                    await remoteStream.CopyToAsync(output, cancellationToken);
                    await SetCurrentFilePhaseAsync(progressNotification, "encoding", force: true);
                }

                var result = await _converter.ConvertAsync(
                    new MediaConversionRequest(
                        sourcePath,
                        tempTarget,
                        kind,
                        TimeSpan.FromSeconds(kind == MediaConversionKind.Image
                            ? _options.CONVERT_IMAGE_TIMEOUT_SECONDS
                            : _options.CONVERT_VIDEO_TIMEOUT_SECONDS),
                        progressNotification.CurrentFileDuration,
                        progress => UpdateMediaProgressAsync(progressNotification, progress)),
                    cancellationToken);
                conversionAttempt.Command = PrepareReportCommand(
                    RedactDownloadAuthorization(result.Command),
                    sourcePath,
                    tempTarget,
                    file.FileName,
                    targetFile);

                var fileInfo = new FileInfo(tempTarget);
                if (fileInfo.Exists)
                {
                    conversionAttempt.ConvertedSize = ContentLengthCoverter.Format(fileInfo.Length);
                }

                if (!result.Success)
                {
                    if (result.Cancelled || cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    var error = string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"ffmpeg exited with code {result.ExitCode}."
                        : RedactDownloadAuthorization(result.StandardError);

                    throw result.TimedOut
                        ? new TimeoutException(error)
                        : new InvalidOperationException(error);
                }

                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("ffmpeg did not produce an output file.");
                }

                if (kind == MediaConversionKind.Video && originalDuration is { } expectedDuration)
                {
                    var outputMetadata = await _converter.ProbeAsync(tempTarget, cancellationToken);
                    if (!outputMetadata.Success || outputMetadata.Metadata?.Duration is not { } actualDuration)
                    {
                        var error = string.IsNullOrWhiteSpace(outputMetadata.Error)
                            ? "Converted video duration could not be verified."
                            : $"Converted video duration could not be verified: {RedactDownloadAuthorization(outputMetadata.Error)}";
                        throw new InvalidOperationException(error);
                    }

                    if (Math.Abs((actualDuration - expectedDuration).TotalMilliseconds) >
                        VideoDurationTolerance.TotalMilliseconds)
                    {
                        throw new InvalidOperationException(
                            $"Converted video duration {FormatDuration(actualDuration)} does not match the original duration {FormatDuration(expectedDuration)}.");
                    }
                }

                if (kind == MediaConversionKind.Video &&
                    sourceSize is > 0 &&
                    fileInfo.Length >= sourceSize.Value)
                {
                    _logger.LogInformation(
                        "Skipping video conversion for {SourceFile}; output size {OutputSize} is not smaller than source size {SourceSize}.",
                        file.FileName,
                        fileInfo.Length,
                        sourceSize.Value);
                    return false;
                }

                await SetCurrentFilePhaseAsync(
                    progressNotification,
                    "uploading",
                    force: true,
                    clearPercent: true);
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

                await SetCurrentFilePhaseAsync(
                    progressNotification,
                    "verifying",
                    force: true,
                    clearPercent: true);
                if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is null)
                {
                    throw new InvalidOperationException(
                        $"Uploaded conversion target '{targetFile}' could not be verified.");
                }

                return true;
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

        private void BeginCurrentFileProgress(ProgressNotificationState state)
        {
            var now = DateTimeOffset.UtcNow;
            state.CurrentFileStartedUtc = now;
            state.CurrentFileDuration = null;

            lock (_syncRoot)
            {
                _report.CurrentFileProgress = new ConversionFileProgress(
                    "preparing",
                    null,
                    0,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    now);
            }
        }

        private async Task SetCurrentFilePhaseAsync(
            ProgressNotificationState state,
            string phase,
            bool force = false,
            bool clearPercent = false,
            double? percent = null)
        {
            var now = DateTimeOffset.UtcNow;
            var updated = false;

            lock (_syncRoot)
            {
                if (_report.CurrentFileProgress is not { } current)
                {
                    return;
                }

                _report.CurrentFileProgress = current with
                {
                    Phase = phase,
                    Percent = clearPercent ? null : percent ?? current.Percent,
                    ElapsedSeconds = GetCurrentFileElapsedSeconds(state, now),
                    DurationSeconds = state.CurrentFileDuration?.TotalSeconds ?? current.DurationSeconds,
                    MediaTimeSeconds = clearPercent ? null : current.MediaTimeSeconds,
                    Speed = clearPercent ? null : current.Speed,
                    Frame = clearPercent ? null : current.Frame,
                    BytesCompleted = clearPercent ? null : current.BytesCompleted,
                    LastUpdatedUtc = now
                };
                updated = true;
            }

            if (updated)
            {
                await PublishProgressAsync(state, force);
            }
        }

        private async ValueTask UpdateMediaProgressAsync(
            ProgressNotificationState state,
            MediaConversionProgress progress)
        {
            var now = DateTimeOffset.UtcNow;
            var percent = progress.Percent ?? (progress.Completed ? 100 : null);

            lock (_syncRoot)
            {
                if (_report.CurrentFileProgress is not { } current)
                {
                    return;
                }

                _report.CurrentFileProgress = current with
                {
                    Phase = "encoding",
                    Percent = percent ?? current.Percent,
                    ElapsedSeconds = GetCurrentFileElapsedSeconds(state, now),
                    MediaTimeSeconds = progress.OutputTime?.TotalSeconds,
                    DurationSeconds = state.CurrentFileDuration?.TotalSeconds ?? current.DurationSeconds,
                    Speed = progress.Speed,
                    Frame = progress.Frame,
                    BytesCompleted = progress.TotalSize,
                    LastUpdatedUtc = now
                };
            }

            await PublishProgressAsync(state);
        }

        private static double GetCurrentFileElapsedSeconds(
            ProgressNotificationState state,
            DateTimeOffset now)
        {
            return state.CurrentFileStartedUtc == DateTimeOffset.MinValue
                ? 0
                : Math.Max(0, (now - state.CurrentFileStartedUtc).TotalSeconds);
        }

        private async Task RecordSkippedAsync(
            B2File file,
            string targetFile,
            string details,
            ProgressNotificationState state,
            string convertedSize = "",
            string command = "")
        {
            RecordSkipped(file, targetFile, details, convertedSize, command);
            await SetCurrentFilePhaseAsync(state, "skipped", clearPercent: true);
        }

        private void RecordSkipped(
            B2File file,
            string targetFile,
            string details,
            string convertedSize = "",
            string command = "")
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
                    details,
                    convertedSize,
                    command));
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.ToString("c", CultureInfo.InvariantCulture);
        }

        private static string PrepareReportCommand(
            string command,
            string actualSourcePath,
            string actualTargetPath,
            string sourceFile,
            string targetFile)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return string.Empty;
            }

            return command
                .Replace(actualSourcePath, sourceFile, StringComparison.Ordinal)
                .Replace(actualTargetPath, targetFile, StringComparison.Ordinal);
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

        private async Task PublishProgressAsync(
            ProgressNotificationState state,
            bool force = false)
        {
            var report = SnapshotReport();
            var now = DateTimeOffset.UtcNow;
            var hasPreviousAttempt = state.LastAttemptUtc != DateTimeOffset.MinValue;
            var elapsed = hasPreviousAttempt
                ? now - state.LastAttemptUtc
                : TimeSpan.MaxValue;
            var scansSinceLastAttempt = report.Scanned - state.LastAttemptedScanned;

            if (!force &&
                hasPreviousAttempt &&
                scansSinceLastAttempt < ProgressNotificationScanInterval &&
                elapsed < ProgressNotificationMinimumInterval)
            {
                return;
            }

            state.LastAttemptedScanned = report.Scanned;
            state.LastAttemptUtc = now;

            try
            {
                await _hubContext.Clients.All.SendAsync("convert", report);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not publish media conversion progress.");
            }
        }

        private void RecordFailure(
            B2File file,
            string targetFile,
            Exception exception,
            ConversionAttemptDetails conversionAttempt)
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
                    RedactDownloadAuthorization(exception.Message),
                    conversionAttempt.ConvertedSize,
                    conversionAttempt.Command));
            }
        }

        private async Task<IReadOnlyList<VideoConversionRequest>> GetPendingRequestsAsync(
            string path,
            CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetService<IVideoConversionRequestStore>();
            return store is null
                ? []
                : await store.GetPendingAsync(path, cancellationToken);
        }

        private async Task<VideoConversionRequest?> EnqueueAutomaticRequestAsync(
            B2File file,
            string userName,
            CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetService<IVideoConversionRequestStore>();
            return store is null
                ? null
                : await store.EnqueueAsync(
                    file.FileName,
                    file.Id,
                    userName,
                    VideoConversionRequestReason.Automatic,
                    cancellationToken);
        }

        private async Task MarkRunningAsync(int? requestId, CancellationToken cancellationToken)
        {
            await ExecuteRequestStoreActionAsync(
                requestId,
                (store, id) => store.MarkRunningAsync(id, cancellationToken));
        }

        private async Task MarkCompletedAsync(int? requestId, CancellationToken cancellationToken)
        {
            await ExecuteRequestStoreActionAsync(
                requestId,
                (store, id) => store.MarkCompletedAsync(id, cancellationToken));
        }

        private async Task MarkFailedAsync(int? requestId, string error, CancellationToken cancellationToken)
        {
            await ExecuteRequestStoreActionAsync(
                requestId,
                (store, id) => store.MarkFailedAsync(id, error, cancellationToken));
        }

        private async Task MarkQueuedAsync(int? requestId, CancellationToken cancellationToken)
        {
            await ExecuteRequestStoreActionAsync(
                requestId,
                (store, id) => store.MarkQueuedAsync(id, cancellationToken));
        }

        private async Task MarkMissingRequestAsync(
            string fileName,
            IReadOnlyList<VideoConversionRequest> pendingRequests)
        {
            var request = pendingRequests.FirstOrDefault(item =>
                string.Equals(item.FileName, fileName, StringComparison.Ordinal));
            if (request is null)
            {
                return;
            }

            await MarkFailedAsync(
                request.Id,
                "The queued source file was not found.",
                CancellationToken.None);
            _logger.LogWarning("Queued conversion source file was not found: {FileName}", fileName);
        }

        private async Task ExecuteRequestStoreActionAsync(
            int? requestId,
            Func<IVideoConversionRequestStore, int, Task> action)
        {
            if (requestId is null)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetService<IVideoConversionRequestStore>();
            if (store is not null)
            {
                await action(store, requestId.Value);
            }
        }

        private async Task<VideoConversionDecision> GetVideoConversionDecisionAsync(
            B2File file,
            string userName,
            IB2Service b2,
            CancellationToken cancellationToken)
        {
            var sourceUrl = await b2.GetDownloadUrl(userName, file.FileName, cancellationToken);
            var metadata = await _converter.ProbeAsync(sourceUrl.AbsoluteUri, cancellationToken);

            if (metadata.HttpStatusCode == 401)
            {
                sourceUrl = await b2.GetDownloadUrl(userName, file.FileName, cancellationToken);
                metadata = await _converter.ProbeAsync(sourceUrl.AbsoluteUri, cancellationToken);
            }

            if (metadata.Cancelled || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!metadata.Success || metadata.Metadata is null)
            {
                var details = string.IsNullOrWhiteSpace(metadata.Error)
                    ? "Video bitrate could not be inspected; use manual conversion selection."
                    : $"Video metadata could not be inspected: {RedactDownloadAuthorization(metadata.Error)}";
                return new VideoConversionDecision(false, false, null, null, details);
            }

            var bitrates = new[] { metadata.Metadata.VideoBitrate, metadata.Metadata.FormatBitrate }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();

            if (bitrates.Length == 0)
            {
                return new VideoConversionDecision(
                    false,
                    false,
                    null,
                    metadata.Metadata.Duration,
                    "Video bitrate metadata is unavailable; use manual conversion selection.");
            }

            var bitrate = bitrates.Max();
            return bitrate > _options.CONVERT_VIDEO_MAX_BITRATE
                ? new VideoConversionDecision(
                    true,
                    true,
                    metadata.Metadata.FileSize,
                    metadata.Metadata.Duration,
                    $"Detected bitrate {FormatBitrate(bitrate)} exceeds the configured limit of {FormatBitrate(_options.CONVERT_VIDEO_MAX_BITRATE)}.")
                : new VideoConversionDecision(
                    false,
                    true,
                    metadata.Metadata.FileSize,
                    metadata.Metadata.Duration,
                    $"Detected bitrate {FormatBitrate(bitrate)} is already within the configured limit of {FormatBitrate(_options.CONVERT_VIDEO_MAX_BITRATE)}; compression can be skipped.");
        }

        private static VideoConversionRequest? FindPendingRequest(
            IReadOnlyList<VideoConversionRequest> requests,
            B2File file)
        {
            return requests.FirstOrDefault(request =>
                string.Equals(request.FileName, file.FileName, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(request.FileId) ||
                 string.Equals(request.FileId, file.Id, StringComparison.Ordinal)));
        }

        private static string FormatBitrate(long bitrate)
        {
            return bitrate >= 1_000_000
                ? $"{bitrate / 1_000_000d:0.##} Mbps"
                : $"{bitrate / 1_000d:0.##} kbps";
        }

        private static string RedactDownloadAuthorization(string error)
        {
            return Regex.Replace(
                error,
                @"(?i)([?&]Authorization=)[^&\s]+",
                "$1[redacted]");
        }

        private sealed record VideoConversionDecision(
            bool ShouldConvert,
            bool HasKnownBitrate,
            long? SourceSize,
            TimeSpan? Duration,
            string Details);

        private sealed class ConversionAttemptDetails
        {
            public string ConvertedSize { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
        }

        private sealed class ProgressNotificationState
        {
            public int LastAttemptedScanned { get; set; }
            public DateTimeOffset LastAttemptUtc { get; set; } = DateTimeOffset.MinValue;
            public DateTimeOffset CurrentFileStartedUtc { get; set; } = DateTimeOffset.MinValue;
            public TimeSpan? CurrentFileDuration { get; set; }
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
                    ContinueAfterRunAsync,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }

        private async Task ContinueAfterRunAsync(Task completedTask)
        {
            if (completedTask.Exception is not null)
            {
                _logger.LogError(completedTask.Exception, "Conversion background task failed.");
            }

            if (SnapshotReport().Status == "cancelled")
            {
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var store = scope.ServiceProvider.GetService<IVideoConversionRequestStore>();
                if (store is null)
                {
                    return;
                }

                var nextRequest = (await store.GetPendingAsync(null, CancellationToken.None))
                    .Where(request =>
                        (request.Status is VideoConversionRequestStatus.Queued or VideoConversionRequestStatus.Running) &&
                        request.Reason == VideoConversionRequestReason.Manual)
                    .OrderBy(request => request.Id)
                    .FirstOrDefault();

                if (nextRequest is not null)
                {
                    TryStartForFile(nextRequest.RequestedBy, nextRequest.FileName);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not continue the manually queued conversion requests.");
            }
        }
    }
}
