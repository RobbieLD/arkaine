using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Media;
using System.Text;

namespace Server.Arkaine.Tests
{
    public class ConversionManagerTests
    {
        [Test]
        public async Task ConvertAsync_OnlyProcessesFilesInSelectedPathAndLeavesSourceUntouched()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var bytes = Encoding.UTF8.GetBytes("image");

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object("gallery-alpha/photo-01.webp", "image/webp", bytes, "source-01"),
                new MockB2.MockB2Object("gallery-beta/photo-02.webp", "image/webp", bytes, "source-02")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            var b2 = new RecordingB2Service(mockStore);
            using var services = BuildServices(options, converter, mockStore, b2);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(
                b2.Requests.Any(request =>
                    request.ExactFileName is null &&
                    request.Prefix == "gallery-alpha/"),
                Is.True);
            Assert.That(files, Does.Contain("gallery-alpha/photo-01.jpg"));
            Assert.That(files, Does.Contain("gallery-alpha/photo-01.webp"));
            Assert.That(files, Does.Contain("gallery-beta/photo-02.webp"));
            Assert.That(files, Does.Not.Contain("gallery-beta/photo-02.jpg"));
            Assert.That(manager.GetStatus().Report.Converted, Is.EqualTo(1));
            Assert.That(Directory.Exists(Path.Combine(options.THUMBNAIL_DIR, ".conversion-state")), Is.False);
        }

        [Test]
        public async Task ConvertAsync_PublishesCurrentFileBeforeLongConversionCompletes()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            const string source = "gallery-alpha/photo-01.webp";
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")],
                options.THUMBNAIL_DIR);
            var conversionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowConversionToFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var converter = new StubMediaConverter
            {
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    if (request.Progress is not null)
                    {
                        await request.Progress(new MediaConversionProgress(
                            10,
                            TimeSpan.FromSeconds(5),
                            1.5,
                            1000,
                            null,
                            false));
                    }

                    conversionStarted.TrySetResult(true);
                    await allowConversionToFinish.Task.WaitAsync(cancellationToken);
                    await File.WriteAllTextAsync(request.TargetPath, "converted", cancellationToken);
                    return new Server.Arkaine.Media.MediaConversionResult(
                        true,
                        0,
                        string.Empty,
                        TimeSpan.Zero,
                        false,
                        false);
                }
            };

            using var services = BuildServices(options, converter, mockStore);
            var hub = services.GetRequiredService<RecordingHubContext<AdminHub>>();
            var manager = services.GetRequiredService<ConversionManager>();

            try
            {
                Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
                await conversionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                var reports = hub.TypedClients.Messages
                    .Where(message => message.Method == "convert")
                    .Select(message => message.Args.SingleOrDefault())
                    .OfType<ConversionReport>()
                    .ToList();

                Assert.That(
                    reports.Any(report =>
                        !report.Finished &&
                        report.CurrentFile == source),
                    Is.True);
                var currentProgress = manager.GetStatus().Report.CurrentFileProgress;
                Assert.That(currentProgress, Is.Not.Null);
                Assert.That(currentProgress!.Phase, Is.EqualTo("encoding"));
                Assert.That(currentProgress.MediaTimeSeconds, Is.EqualTo(5));
                Assert.That(currentProgress.Speed, Is.EqualTo(1.5));
                Assert.That(currentProgress.Frame, Is.EqualTo(10));
                Assert.That(currentProgress.BytesCompleted, Is.EqualTo(1000));
                Assert.That(currentProgress.ElapsedSeconds, Is.GreaterThan(0));
                Assert.That(currentProgress.LastUpdatedUtc, Is.Not.EqualTo(default(DateTimeOffset)));
            }
            finally
            {
                allowConversionToFinish.TrySetResult(true);
                await manager.WaitForCompletionAsync();
            }
        }

        [Test]
        public async Task ConvertAsync_RootPathUsesEmptyB2Prefix()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var bytes = Encoding.UTF8.GetBytes("image");

            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object("photo.webp", "image/webp", bytes, "source-01")],
                options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            var b2 = new RecordingB2Service(mockStore);
            using var services = BuildServices(options, converter, mockStore, b2);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", ConversionPath.RootSelection), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(
                b2.Requests.Any(request =>
                    request.ExactFileName is null &&
                    request.Prefix == string.Empty),
                Is.True);
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain("photo.jpg"));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain("photo.webp"));
        }

        [Test]
        public async Task ConvertAsync_SkipsExistingTargetAndLeavesSourceUntouched()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var bytes = Encoding.UTF8.GetBytes("image");
            const string source = "gallery-alpha/photo-01.webp";
            const string target = "gallery-alpha/photo-01.jpg";

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object(source, "image/webp", bytes, "source-01"),
                new MockB2.MockB2Object(target, "image/jpeg", bytes, "target-01")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            var reports = new RecordingProcessingReportService();
            using var services = BuildServices(options, converter, mockStore, reportService: reports);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain(source));
            Assert.That(files, Does.Contain(target));
            Assert.That(converter.Requests, Is.Empty);
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(2));
            Assert.That(reports.Saved, Has.Count.EqualTo(1));
            Assert.That(reports.Saved[0].Type, Is.EqualTo(ProcessingReportType.Conversion));
            Assert.That(reports.Saved[0].Html, Does.Not.Contain("gallery-alpha/photo-01.webp"));
            Assert.That(reports.Saved[0].Html, Does.Not.Contain("The destination file already exists."));
        }

        [Test]
        public async Task ConvertAsync_FailedConversionCanBeRetriedWithoutStateMarkers()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            const string source = "gallery-alpha/photo-01.webp";
            const string target = "gallery-alpha/photo-01.jpg";
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")],
                options.THUMBNAIL_DIR);

            var attempts = 0;
            var converter = new StubMediaConverter
            {
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        return new Server.Arkaine.Media.MediaConversionResult(
                            false,
                            1,
                            "unsupported input",
                            TimeSpan.Zero,
                            false,
                            false);
                    }

                    await File.WriteAllTextAsync(request.TargetPath, "converted", cancellationToken);
                    return new Server.Arkaine.Media.MediaConversionResult(
                        true,
                        0,
                        string.Empty,
                        TimeSpan.Zero,
                        false,
                        false);
                }
            };
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(1));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Not.Contain(target));
            Assert.That(Directory.Exists(Path.Combine(options.THUMBNAIL_DIR, ".conversion-state")), Is.False);

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(source));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(target));
            Assert.That(manager.GetStatus().Report.Converted, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_SkipsUnsupportedKeysWithoutDeletingThem()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            const string source = "gallery-alpha/ready.mp4";

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object(source, "video/mp4", Encoding.UTF8.GetBytes("video"), "source-01")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Is.Empty);
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(source));
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_ConvertsSupportedVideoWhenMetadataExceedsConfiguredBitrate()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object("gallery-alpha/ready.mp4", "video/mp4", Encoding.UTF8.GetBytes("video"), "source-01")],
                options.THUMBNAIL_DIR);
            var converter = new StubMediaConverter
            {
                ProbeResult = new MediaMetadataResult(
                    true,
                    new MediaMetadata(
                        TimeSpan.FromMinutes(2),
                        12_000_000,
                        10_000_000,
                        "h264",
                        3840,
                        2160,
                        30,
                        192_000,
                        "aac"),
                    string.Empty,
                    TimeSpan.Zero,
                    false,
                    false)
            };

            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", ConversionPath.RootSelection), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(converter.ProbeRequests, Has.Count.EqualTo(2));
            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(converter.Requests[0].SourcePath, Does.StartWith("https://mock-b2.invalid/"));
            Assert.That(files, Does.Contain("gallery-alpha/ready.mp4"));
            Assert.That(files, Does.Contain("gallery-alpha/ready_compressed.mp4"));
            Assert.That(manager.GetStatus().Report.Converted, Is.EqualTo(1));

            using var scope = services.CreateScope();
            var requests = await scope.ServiceProvider
                .GetRequiredService<IVideoConversionRequestStore>()
                .ListAsync(VideoConversionRequestStatus.Completed, CancellationToken.None);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Reason, Is.EqualTo(VideoConversionRequestReason.Automatic));
        }

        [Test]
        public async Task ConvertAsync_RecordsDurationMismatchAsConversionError()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            const string source = "gallery-alpha/ready.mp4";
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "video/mp4", Encoding.UTF8.GetBytes("video"), "source-01")],
                options.THUMBNAIL_DIR);
            var sourceMetadata = new MediaMetadataResult(
                true,
                new MediaMetadata(
                    TimeSpan.FromMinutes(2),
                    12_000_000,
                    10_000_000,
                    "h264",
                    3840,
                    2160,
                    30,
                    192_000,
                    "aac"),
                string.Empty,
                TimeSpan.Zero,
                false,
                false);
            var convertedMetadata = sourceMetadata with
            {
                Metadata = sourceMetadata.Metadata! with
                {
                    Duration = TimeSpan.FromSeconds(90)
                }
            };
            var converter = new StubMediaConverter
            {
                OnProbeAsync = (path, _) => Task.FromResult(
                    path.StartsWith("https://", StringComparison.Ordinal)
                        ? sourceMetadata
                        : convertedMetadata),
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    await File.WriteAllTextAsync(request.TargetPath, "converted", cancellationToken);
                    return new MediaConversionResult(
                        true,
                        0,
                        string.Empty,
                        TimeSpan.Zero,
                        false,
                        false,
                        Command: "ffmpeg -i source.mp4 target.mp4");
                }
            };
            var reports = new RecordingProcessingReportService();

            using var services = BuildServices(options, converter, mockStore, reportService: reports);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", ConversionPath.RootSelection), Is.True);
            await manager.WaitForCompletionAsync();

            var report = manager.GetStatus().Report;
            Assert.That(report.Converted, Is.EqualTo(0));
            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That(report.Failures, Has.Count.EqualTo(1));
            Assert.That(report.Failures[0].Error, Does.Contain("does not match"));
            Assert.That(reports.Saved, Has.Count.EqualTo(1));
            Assert.That(reports.Saved[0].Html, Does.Contain("Converted video duration"));
            Assert.That(reports.Saved[0].Html, Does.Contain("Original size"));
            Assert.That(reports.Saved[0].Html, Does.Contain("Converted size"));
            Assert.That(reports.Saved[0].Html, Does.Contain("9 B"));
            Assert.That(reports.Saved[0].Html, Does.Contain("ffmpeg -i source.mp4 target.mp4"));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Not.Contain("gallery-alpha/ready_compressed.mp4"));
        }

        [Test]
        public async Task ConvertAsync_SkipsQueuedVideoWhenMetadataIsAlreadyWithinBitrateLimit()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object("gallery-alpha/ready.mp4", "video/mp4", Encoding.UTF8.GetBytes("video"), "source-01")],
                options.THUMBNAIL_DIR);
            var converter = new StubMediaConverter
            {
                ProbeResult = new MediaMetadataResult(
                    true,
                    new MediaMetadata(
                        TimeSpan.FromMinutes(2),
                        2_000_000,
                        1_800_000,
                        "h264",
                        1920,
                        1080,
                        30,
                        128_000,
                        "aac"),
                    string.Empty,
                    TimeSpan.Zero,
                    false,
                    false)
            };

            using var services = BuildServices(options, converter, mockStore);
            using (var scope = services.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IVideoConversionRequestStore>()
                    .EnqueueAsync(
                        "gallery-alpha/ready.mp4",
                        "source-01",
                        "admin",
                        VideoConversionRequestReason.Manual,
                        CancellationToken.None);
            }

            var manager = services.GetRequiredService<ConversionManager>();
            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.ProbeRequests, Has.Count.EqualTo(1));
            Assert.That(converter.Requests, Is.Empty);
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Not.Contain("gallery-alpha/ready_compressed.mp4"));
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(1));

            using var resultScope = services.CreateScope();
            var requests = await resultScope.ServiceProvider
                .GetRequiredService<IVideoConversionRequestStore>()
                .ListAsync(VideoConversionRequestStatus.Completed, CancellationToken.None);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Reason, Is.EqualTo(VideoConversionRequestReason.Manual));
        }

        [Test]
        public async Task ConvertAsync_ForManuallyQueuedFileListsOnlyThatFile()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var selectedFile = "gallery-alpha/selected.mp4";
            var otherFile = "gallery-alpha/other.mp4";
            var mockStore = CreateMockStore(
                [
                    new MockB2.MockB2Object(selectedFile, "video/mp4", Encoding.UTF8.GetBytes("selected"), "source-01"),
                    new MockB2.MockB2Object(otherFile, "video/mp4", Encoding.UTF8.GetBytes("other"), "source-02")
                ],
                options.THUMBNAIL_DIR);
            var converter = new StubMediaConverter();
            var b2 = new RecordingB2Service(mockStore);

            using var services = BuildServices(options, converter, mockStore, b2);
            using (var scope = services.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IVideoConversionRequestStore>()
                    .EnqueueAsync(
                        selectedFile,
                        "source-01",
                        "admin",
                        VideoConversionRequestReason.Manual,
                        CancellationToken.None);
            }

            var manager = services.GetRequiredService<ConversionManager>();
            Assert.That(manager.TryStartForFile("admin", selectedFile), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(b2.Requests[0].ExactFileName, Is.EqualTo(selectedFile));
            Assert.That(b2.Requests[0].Prefix, Is.Null);
            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain($"{selectedFile[..^4]}_compressed.mp4"));
            Assert.That(files, Does.Not.Contain($"{otherFile[..^4]}_compressed.mp4"));
            Assert.That(converter.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_SkipsOutputWhenTranscodeWouldBeLargerThanSource()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var source = "gallery-alpha/ready.mp4";
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "video/mp4", Encoding.UTF8.GetBytes("source"), "source-01")],
                options.THUMBNAIL_DIR);
            var converter = new StubMediaConverter
            {
                ProbeResult = new MediaMetadataResult(
                    true,
                    new MediaMetadata(
                        TimeSpan.FromMinutes(2),
                        12_000_000,
                        10_000_000,
                        "h264",
                        3840,
                        2160,
                        30,
                        192_000,
                        "aac",
                        6),
                    string.Empty,
                    TimeSpan.Zero,
                    false,
                    false),
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    await File.WriteAllTextAsync(request.TargetPath, "this output is larger", cancellationToken);
                    return new MediaConversionResult(true, 0, string.Empty, TimeSpan.Zero, false, false);
                }
            };

            using var services = BuildServices(options, converter, mockStore);
            using (var scope = services.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IVideoConversionRequestStore>()
                    .EnqueueAsync(
                        source,
                        "source-01",
                        "admin",
                        VideoConversionRequestReason.Manual,
                        CancellationToken.None);
            }

            var manager = services.GetRequiredService<ConversionManager>();
            Assert.That(manager.TryStartForFile("admin", source), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Not.Contain("gallery-alpha/ready_compressed.mp4"));
            Assert.That(manager.GetStatus().Report.Converted, Is.EqualTo(0));
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_DoesNotDeleteUnrelatedDirectoriesInTempRoot()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var unrelatedDirectory = Path.Combine(options.CONVERSION_TEMP_DIR, "unrelated");
            Directory.CreateDirectory(unrelatedDirectory);
            var unrelatedFile = Path.Combine(unrelatedDirectory, "keep.txt");
            await File.WriteAllTextAsync(unrelatedFile, "keep");
            var mockStore = CreateMockStore([], options.THUMBNAIL_DIR);

            using var services = BuildServices(options, new StubMediaConverter(), mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(File.Exists(unrelatedFile), Is.True);
        }

        [Test]
        public async Task TryStart_PreventsConcurrentRuns_AndCancelMarksTheReport()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object("gallery-alpha/video-01.mov", "video/quicktime", Encoding.UTF8.GetBytes("video"), "source-01")],
                options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter
            {
                OnConvertAsync = async (_, cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return new Server.Arkaine.Media.MediaConversionResult(true, 0, string.Empty, TimeSpan.Zero, false, false);
                }
            };

            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.False);

            manager.Cancel();
            await manager.WaitForCompletionAsync();

            var report = manager.GetStatus().Report;
            Assert.That(report.Cancelled, Is.True);
            Assert.That(report.Status, Is.EqualTo("cancelled"));
            Assert.That(report.Finished, Is.True);
        }

        private static ServiceProvider BuildServices(
            ArkaineOptions options,
            StubMediaConverter converter,
            MockB2.Store mockStore,
            IB2Service? b2Override = null,
            IProcessingReportService? reportService = null)
        {
            var services = new ServiceCollection();
            var hub = new RecordingHubContext<AdminHub>();
            var databaseName = $"conversion-{Guid.NewGuid():N}";

            services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            services.AddDbContext<ArkaineDbContext>(builder =>
                builder.UseInMemoryDatabase(databaseName));
            services.AddScoped<IVideoConversionRequestStore, VideoConversionRequestStore>();
            services.AddSingleton<IHubContext<AdminHub>>(hub);
            services.AddSingleton(hub);
            services.AddSingleton<IThumbnailInfoProvider, ThumbnailInfoCache>();
            services.AddSingleton<AdminJobCoordinator>();
            services.AddSingleton<Server.Arkaine.Media.IMediaConverter>(converter);
            services.AddSingleton(mockStore);
            services.AddSingleton<IProcessingReportService>(
                reportService ?? new NoOpProcessingReportService());
            if (b2Override is null)
            {
                services.AddScoped<IB2Service, MockB2>();
            }
            else
            {
                services.AddScoped(_ => b2Override);
            }
            services.AddLogging();
            services.AddSingleton<ConversionManager>();

            return services.BuildServiceProvider();
        }

        private static MockB2.Store CreateMockStore(
            IEnumerable<MockB2.MockB2Object> objects,
            string thumbnailDirectory)
        {
            var store = new MockB2.Store();
            store.Reset(objects, thumbnailDirectory);
            return store;
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class RecordingB2Service : IB2Service
        {
            private readonly MockB2 _inner;

            public RecordingB2Service(MockB2.Store store)
            {
                _inner = new MockB2(new ThumbnailInfoCache(), store);
            }

            public List<FilesRequest> Requests { get; } = [];

            public Task Delete(DeleteModel request, CancellationToken cancellationToken) =>
                _inner.Delete(request, cancellationToken);

            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) =>
                _inner.Download(userName, fileName, cancellationToken);

            public Task<Uri> GetDownloadUrl(string userName, string fileName, CancellationToken cancellationToken) =>
                _inner.GetDownloadUrl(userName, fileName, cancellationToken);

            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) =>
                _inner.GetToken(key, cancellationToken);

            public Task Copy(CopyRequest request, CancellationToken cancellationToken) =>
                _inner.Copy(request, cancellationToken);

            public Task<FilesResponse> ListFiles(
                FilesRequest request,
                string userName,
                IFavouritesService? favouritesService,
                CancellationToken cancellationToken)
            {
                Requests.Add(new FilesRequest
                {
                    BucketId = request.BucketId,
                    Delimiter = request.Delimiter,
                    ExactFileName = request.ExactFileName,
                    PageSize = request.PageSize,
                    Prefix = request.Prefix,
                    StartFile = request.StartFile
                });

                return _inner.ListFiles(request, userName, favouritesService, cancellationToken);
            }

            public IResult Preview(string fileName) => _inner.Preview(fileName);

            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) =>
                _inner.Stream(userName, fileName, cancellationToken);

            public Task UploadMultiPartFile(
                string fileName,
                string contentType,
                Stream content,
                int chunkSize,
                CancellationToken cancellationToken) =>
                _inner.UploadMultiPartFile(fileName, contentType, content, chunkSize, cancellationToken);

            public Task UploadSingleFile(
                string fileName,
                string contentType,
                long length,
                Stream content,
                CancellationToken cancellationToken) =>
                _inner.UploadSingleFile(fileName, contentType, length, content, cancellationToken);
        }
    }
}
