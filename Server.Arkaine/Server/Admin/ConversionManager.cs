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
        private readonly IConversionStateStore _stateStore;
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
            IConversionStateStore stateStore,
            IMediaConverter converter,
            AdminJobCoordinator jobCoordinator,
            ILogger<ConversionManager> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _options.Normalize();
            _hubContext = hubContext;
            _stateStore = stateStore;
            _converter = converter;
            _jobCoordinator = jobCoordinator;
            _logger = logger;
        }

        public bool TryStart(string userName, string? exactTarget)
        {
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
                    Directory.CreateDirectory(_options.GetConversionTempDirectory());
                    Directory.CreateDirectory(_stateStore.MarkerDirectory);

                    _stoppingToken?.Dispose();
                    _stoppingToken = new CancellationTokenSource();
                    _report = new ConversionReport
                    {
                        ExactTarget = NormalizePath(exactTarget),
                        Running = true,
                        StartedUtc = DateTimeOffset.UtcNow,
                        Status = "running"
                    };
                    _runningTask = RunAsync(userName, _report.ExactTarget, _stoppingToken.Token);
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

        public async Task ConvertAsync(string userName, string? exactTarget)
        {
            if (!TryStart(userName, exactTarget))
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

        private async Task RunAsync(string userName, string exactTarget, CancellationToken cancellationToken)
        {
            try
            {
                await RecoverPendingStatesAsync(userName, exactTarget, cancellationToken);

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
                    Prefix = _options.GetConversionScanPrefix(exactTarget)
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var b2 = scope.ServiceProvider.GetRequiredService<IB2Service>();
                    var references = scope.ServiceProvider.GetRequiredService<IMediaLibraryReferenceService>();
                    var page = await b2.ListFiles(request, userName, null, cancellationToken);

                    await ProcessPageAsync(page, exactTarget, userName, b2, references, cancellationToken);
                    request.StartFile = page.NextFileName;

                    if (string.IsNullOrEmpty(page.NextFileName) ||
                        (!string.IsNullOrWhiteSpace(exactTarget) && SnapshotReport().Converted + SnapshotReport().Recovered > 0))
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

                _jobCoordinator.Release(AdminJobKind.Conversion);
                await _hubContext.Clients.All.SendAsync("convert", SnapshotReport());
            }
        }

        private async Task RecoverPendingStatesAsync(
            string userName,
            string exactTarget,
            CancellationToken cancellationToken)
        {
            var markers = _stateStore.LoadAll();
            if (markers.Count == 0)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var b2 = scope.ServiceProvider.GetRequiredService<IB2Service>();
            var references = scope.ServiceProvider.GetRequiredService<IMediaLibraryReferenceService>();

            foreach (var marker in markers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!MatchesExactTarget(marker.SourceFile, marker.TargetFile, exactTarget))
                {
                    continue;
                }

                if (!marker.ThumbnailMoved &&
                    !CanManageThumbnailArtifacts(marker.SourceFile, marker.TargetFile))
                {
                    RecordFailure(
                        marker.SourceFile,
                        marker.TargetFile,
                        new InvalidOperationException(
                            $"The B2 key '{marker.SourceFile}' cannot be represented safely in the thumbnail cache."));
                    continue;
                }

                try
                {
                    var targetFile = await GetExactFileAsync(b2, userName, marker.TargetFile, cancellationToken);
                    if (targetFile is not null)
                    {
                        marker.TargetUploaded = true;
                        _stateStore.Save(marker);
                    }
                    else if (!marker.TargetUploaded)
                    {
                        var sourceFile = await GetExactFileAsync(b2, userName, marker.SourceFile, cancellationToken);
                        if (sourceFile is null)
                        {
                            throw new InvalidOperationException($"Pending conversion source '{marker.SourceFile}' was not found.");
                        }

                        marker.SourceId = string.IsNullOrWhiteSpace(marker.SourceId) ? sourceFile.Id : marker.SourceId;
                        await ConvertAndUploadAsync(sourceFile, marker, userName, b2, cancellationToken);
                    }
                    else
                    {
                        marker.TargetUploaded = false;
                        _stateStore.Save(marker);
                        var sourceFile = await GetExactFileAsync(b2, userName, marker.SourceFile, cancellationToken);
                        if (sourceFile is null)
                        {
                            throw new InvalidOperationException(
                                $"Pending conversion target '{marker.TargetFile}' is missing and source '{marker.SourceFile}' was not found.");
                        }

                        await ConvertAndUploadAsync(sourceFile, marker, userName, b2, cancellationToken);
                    }

                    await FinalizeConversionAsync(marker, userName, b2, references, cancellationToken, recovered: true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (PermanentConversionException exception)
                {
                    _stateStore.Delete(marker);
                    RecordFailure(marker.SourceFile, marker.TargetFile, exception);
                }
                catch (Exception exception)
                {
                    RecordFailure(marker.SourceFile, marker.TargetFile, exception);
                }
            }
        }

        private async Task ProcessPageAsync(
            FilesResponse page,
            string exactTarget,
            string userName,
            IB2Service b2,
            IMediaLibraryReferenceService references,
            CancellationToken cancellationToken)
        {
            foreach (var file in page.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_options.IsConvertibleImage(file.FileName) &&
                    !_options.IsConvertibleVideo(file.FileName))
                {
                    lock (_syncRoot)
                    {
                        _report.Scanned++;
                        _report.CurrentFile = file.FileName;
                        _report.Skipped++;
                    }
                    continue;
                }

                var targetFile = _options.GetConversionTargetFileName(file.FileName);

                lock (_syncRoot)
                {
                    _report.Scanned++;
                    _report.CurrentFile = file.FileName;
                }

                if (!CanManageThumbnailArtifacts(file.FileName, targetFile))
                {
                    RecordFailure(
                        file.FileName,
                        targetFile,
                        new InvalidOperationException(
                            $"The B2 key '{file.FileName}' cannot be represented safely in the thumbnail cache."));
                    continue;
                }

                if (string.Equals(file.FileName, targetFile, StringComparison.Ordinal))
                {
                    lock (_syncRoot)
                    {
                        _report.Skipped++;
                    }
                    continue;
                }

                if (HasPermanentFailureMarker(file.FileName))
                {
                    lock (_syncRoot)
                    {
                        _report.Skipped++;
                    }
                    continue;
                }

                if (!MatchesExactTarget(file.FileName, targetFile, exactTarget))
                {
                    lock (_syncRoot)
                    {
                        _report.Skipped++;
                    }
                    continue;
                }

                var marker = new ConversionStateMarker
                {
                    MarkerId = ConversionStateMarker.CreateMarkerId(file.FileName, targetFile),
                    SourceFile = file.FileName,
                    SourceId = file.Id,
                    TargetFile = targetFile
                };

                try
                {
                    if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is not null)
                    {
                        lock (_syncRoot)
                        {
                            _report.Skipped++;
                        }
                        continue;
                    }

                    _stateStore.Save(marker);
                    await ConvertAndUploadAsync(file, marker, userName, b2, cancellationToken);
                    await FinalizeConversionAsync(marker, userName, b2, references, cancellationToken, recovered: false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (PermanentConversionException exception)
                {
                    _stateStore.Delete(marker);
                    RecordFailure(file.FileName, targetFile, exception);
                    await _hubContext.Clients.All.SendAsync("convert", SnapshotReport(), cancellationToken);
                }
                catch (Exception exception)
                {
                    RecordFailure(file.FileName, targetFile, exception);

                    await _hubContext.Clients.All.SendAsync("convert", SnapshotReport(), cancellationToken);
                }

                if (SnapshotReport().Scanned % 25 == 0 || SnapshotReport().Converted > 0 || SnapshotReport().Recovered > 0)
                {
                    await _hubContext.Clients.All.SendAsync("convert", SnapshotReport(), cancellationToken);
                }
            }
        }

        private async Task ConvertAndUploadAsync(
            B2File file,
            ConversionStateMarker marker,
            string userName,
            IB2Service b2,
            CancellationToken cancellationToken)
        {
            var kind = _options.IsConvertibleImage(file.FileName)
                ? MediaConversionKind.Image
                : MediaConversionKind.Video;
            var targetFile = marker.TargetFile;
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

                    if (result.TimedOut)
                    {
                        throw new TimeoutException(error);
                    }

                    WritePermanentFailureMarker(file.FileName, error);
                    throw new PermanentConversionException(error);
                }

                var fileInfo = new FileInfo(tempTarget);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    const string error = "ffmpeg did not produce an output file.";
                    WritePermanentFailureMarker(file.FileName, error);
                    throw new PermanentConversionException(error);
                }

                await using var convertedStream = File.OpenRead(tempTarget);
                var contentType = kind == MediaConversionKind.Image ? "image/jpeg" : "video/mp4";

                if (fileInfo.Length > _options.UPLOAD_CHUNK_SIZE)
                {
                    await b2.UploadMultiPartFile(targetFile, contentType, convertedStream, _options.UPLOAD_CHUNK_SIZE, cancellationToken);
                }
                else
                {
                    await b2.UploadSingleFile(targetFile, contentType, fileInfo.Length, convertedStream, cancellationToken);
                }

                if (await GetExactFileAsync(b2, userName, targetFile, cancellationToken) is null)
                {
                    throw new InvalidOperationException(
                        $"Uploaded conversion target '{targetFile}' could not be verified.");
                }

                marker.TargetUploaded = true;
                _stateStore.Save(marker);
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private async Task FinalizeConversionAsync(
            ConversionStateMarker marker,
            string userName,
            IB2Service b2,
            IMediaLibraryReferenceService references,
            CancellationToken cancellationToken,
            bool recovered)
        {
            if (!marker.TargetUploaded)
            {
                throw new InvalidOperationException($"Converted target '{marker.TargetFile}' has not been uploaded.");
            }

            _stateStore.Save(marker);

            if (!marker.ReferencesRenamed)
            {
                await references.RenameFileReferencesAsync(marker.SourceFile, marker.TargetFile, cancellationToken);
                marker.ReferencesRenamed = true;
                _stateStore.Save(marker);
            }

            if (!marker.ThumbnailMoved)
            {
                MoveThumbnailArtifacts(marker.SourceFile, marker.TargetFile);
                marker.ThumbnailMoved = true;
                _stateStore.Save(marker);
            }

            if (!marker.SourceDeleted)
            {
                var sourceFile = await GetExactFileAsync(b2, userName, marker.SourceFile, cancellationToken);
                if (sourceFile is null)
                {
                    marker.SourceDeleted = true;
                }
                else
                {
                    marker.SourceId = string.IsNullOrWhiteSpace(marker.SourceId) ? sourceFile.Id : marker.SourceId;
                    await b2.Delete(new DeleteModel
                    {
                        FileName = marker.SourceFile,
                        Id = marker.SourceId
                    }, cancellationToken);
                    marker.SourceDeleted = true;
                }

                _stateStore.Save(marker);
            }

            _stateStore.Delete(marker);

            lock (_syncRoot)
            {
                if (recovered)
                {
                    _report.Recovered++;
                }
                else
                {
                    _report.Converted++;
                }
            }
        }

        private void MoveThumbnailArtifacts(string sourceFile, string targetFile)
        {
            MoveThumbnailArtifact(sourceFile, targetFile);
            DeleteThumbnailArtifact($"{sourceFile}.bad");
            DeleteThumbnailArtifact($"{targetFile}.bad");
        }

        private void DeleteThumbnailArtifact(string relativePath)
        {
            if (!ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, relativePath, out var path))
            {
                throw new InvalidOperationException($"Unable to resolve thumbnail state for {relativePath}.");
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private bool HasPermanentFailureMarker(string sourceFile)
        {
            return ThumbnailPathResolver.TryResolve(
                       _options.THUMBNAIL_DIR,
                       $"{sourceFile}.convert-failed",
                       out var markerPath) &&
                   File.Exists(markerPath);
        }

        private bool CanManageThumbnailArtifacts(string sourceFile, string targetFile)
        {
            return IsCanonicalThumbnailKey(sourceFile) &&
                   IsCanonicalThumbnailKey(targetFile) &&
                   ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, sourceFile, out _) &&
                   ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, targetFile, out _) &&
                   ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, $"{sourceFile}.bad", out _) &&
                   ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, $"{targetFile}.bad", out _) &&
                   ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, $"{sourceFile}.convert-failed", out _);
        }

        private static bool IsCanonicalThumbnailKey(string fileName)
        {
            return !fileName.Contains('\\') &&
                   fileName.Split('/').All(segment => segment.Length > 0);
        }

        private void WritePermanentFailureMarker(string sourceFile, string error)
        {
            if (!ThumbnailPathResolver.TryResolve(
                    _options.THUMBNAIL_DIR,
                    $"{sourceFile}.convert-failed",
                    out var markerPath))
            {
                throw new InvalidOperationException($"Unable to resolve conversion failure marker for {sourceFile}.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ??
                                      throw new InvalidOperationException("Conversion failure marker directory is invalid."));
            File.WriteAllText(markerPath, $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{error}");
        }

        private void MoveThumbnailArtifact(string sourceRelativePath, string targetRelativePath)
        {
            if (!ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, sourceRelativePath, out var sourcePath) ||
                !ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, targetRelativePath, out var targetPath))
            {
                throw new InvalidOperationException($"Unable to resolve thumbnail state for {sourceRelativePath}.");
            }

            var sourceIsBadMarker = string.Equals(Path.GetExtension(sourcePath), ".bad", StringComparison.OrdinalIgnoreCase);
            var targetMarkerPath = sourceIsBadMarker ? targetPath[..^4] : $"{targetPath}.bad";

            if (!sourceIsBadMarker && File.Exists(targetMarkerPath))
            {
                File.Delete(targetMarkerPath);
            }

            if (sourceIsBadMarker && File.Exists(targetPath[..^4]))
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                return;
            }

            if (!File.Exists(sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? throw new InvalidOperationException("Target thumbnail directory is invalid."));

            if (File.Exists(targetPath))
            {
                File.Delete(sourcePath);
                return;
            }

            File.Move(sourcePath, targetPath);
        }

        private async Task<B2File?> GetExactFileAsync(IB2Service b2, string userName, string fileName, CancellationToken cancellationToken)
        {
            var response = await b2.ListFiles(new FilesRequest
            {
                BucketId = _options.BUCKET_ID,
                ExactFileName = fileName,
                PageSize = 1
            }, userName, null, cancellationToken);

            return response.Files.SingleOrDefault();
        }

        private static bool MatchesExactTarget(string sourceFile, string targetFile, string exactTarget)
        {
            if (string.IsNullOrWhiteSpace(exactTarget))
            {
                return true;
            }

            return string.Equals(sourceFile, exactTarget, StringComparison.Ordinal) ||
                   string.Equals(targetFile, exactTarget, StringComparison.Ordinal);
        }

        private static string NormalizePath(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
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

        private ConversionReport SnapshotReport()
        {
            lock (_syncRoot)
            {
                return _report.Clone();
            }
        }

        private void RecordFailure(string sourceFile, string targetFile, Exception exception)
        {
            _logger.LogError(exception, "Conversion failed for {SourceFile}", sourceFile);
            lock (_syncRoot)
            {
                _report.Failed++;
                _report.Status = "running";
                _report.Failures.Add(new ConversionFailure(sourceFile, targetFile, exception.Message));
            }
        }

        private sealed class PermanentConversionException(string message) : Exception(message);

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
