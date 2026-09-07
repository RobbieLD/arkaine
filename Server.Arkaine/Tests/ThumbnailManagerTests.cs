using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Server.Arkaine.Tests
{
    public class ThumbnailManagerTests
    {
        [Test]
        public void GetStatus_CountsOnlyThumbnailFilesAndIgnoresConversionTemp()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(Path.Combine(options.THUMBNAIL_DIR, "folder"));
            Directory.CreateDirectory(Path.Combine(options.THUMBNAIL_DIR, ".conversion-temp"));
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, "folder", "image.jpg"), []);
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, "folder", "image.jpg.bad"), []);
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, ".conversion-temp", "temp.jpg"), []);

            using var services = BuildServices(options, new NoOpB2Service());
            var manager = services.GetRequiredService<ThumbnailManager>();
            var status = manager.GetStatus();

            Assert.That(status.TotalThumbnails, Is.EqualTo(1));
        }

        [Test]
        public async Task GenerateThumbnails_SkipsAnExistingFile()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var service = new ThumbnailB2Service(CreateImageBytes());
            Assert.That(
                ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, "folder/image.webp", out var thumbnailPath),
                Is.True);
            Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
            await File.WriteAllTextAsync(thumbnailPath, "existing");

            using var services = BuildServices(options, service);
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(service.Downloads, Is.EqualTo(0));
            Assert.That(manager.GetStatus().Report.Generated, Is.EqualTo(0));
            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(0));
        }

        [Test]
        public async Task GenerateThumbnails_PublishesProgressForSkippedFilesBeforeCompletion()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var service = new ThumbnailB2Service(
                CreateImageBytes(),
                [
                    new B2File
                    {
                        FileName = "folder/unsupported.txt",
                        ContentType = "text/plain",
                        Type = "upload"
                    },
                    new B2File
                    {
                        FileName = "folder/existing.webp",
                        ContentType = "image/webp",
                        Type = "upload"
                    }
                ]);
            Assert.That(
                ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, "folder/existing.webp", out var thumbnailPath),
                Is.True);
            Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
            await File.WriteAllTextAsync(thumbnailPath, "existing");

            using var services = BuildServices(options, service);
            var hub = services.GetRequiredService<RecordingHubContext<AdminHub>>();
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            var reports = hub.TypedClients.Messages
                .Where(message => message.Method == "update")
                .Select(message => message.Args.SingleOrDefault())
                .OfType<GenerationReport>()
                .ToList();

            Assert.That(
                reports.Any(report => !report.Finished && report.Scanned == 2),
                Is.True);
            Assert.That(reports.Last().Finished, Is.True);
            Assert.That(reports.Last().Scanned, Is.EqualTo(2));
        }

        [Test]
        public async Task GenerateThumbnails_ExtractsVideoFrameIntoJpegThumbnail()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var service = new ThumbnailB2Service(CreateImageBytes(), "folder/video.mp4", "video/mp4");
            var converter = new StubMediaConverter
            {
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    using var image = new Image<Rgba32>(20, 10);
                    await image.SaveAsJpegAsync(request.TargetPath, cancellationToken);
                    return new MediaConversionResult(true, 0, string.Empty, TimeSpan.Zero, false, false);
                }
            };
            using var services = BuildServices(options, service, converter: converter);
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(
                ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, "folder/video.mp4.jpg", out var thumbnailPath),
                Is.True);
            Assert.That(File.Exists(thumbnailPath), Is.True);
            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(converter.Requests[0].Kind, Is.EqualTo(MediaConversionKind.Image));
            Assert.That(converter.Requests[0].SourcePath, Does.StartWith("https://example.invalid/"));
            Assert.That(service.Downloads, Is.EqualTo(0));
            Assert.That(service.DownloadUrlRequests, Is.EqualTo(1));
            Assert.That(manager.GetStatus().Report.Generated, Is.EqualTo(1));
        }

        [Test]
        public async Task GenerateThumbnails_RetriesVideoFrameWhenRemoteAuthorizationExpires()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var service = new ThumbnailB2Service(CreateImageBytes(), "folder/video.mp4", "video/mp4");
            var conversionAttempts = 0;
            var converter = new StubMediaConverter
            {
                OnConvertAsync = async (request, cancellationToken) =>
                {
                    if (++conversionAttempts == 1)
                    {
                        return new MediaConversionResult(
                            false,
                            1,
                            "HTTP error 401 Unauthorized",
                            TimeSpan.Zero,
                            false,
                            false,
                            401);
                    }

                    using var image = new Image<Rgba32>(20, 10);
                    await image.SaveAsJpegAsync(request.TargetPath, cancellationToken);
                    return new MediaConversionResult(true, 0, string.Empty, TimeSpan.Zero, false, false);
                }
            };
            using var services = BuildServices(options, service, converter: converter);
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Has.Count.EqualTo(2));
            Assert.That(service.DownloadUrlRequests, Is.EqualTo(2));
            Assert.That(manager.GetStatus().Report.Generated, Is.EqualTo(1));
            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(0));
        }

        [Test]
        public async Task GenerateThumbnails_RetriesFailedGenerationWithoutBadMarker()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var service = new ThumbnailB2Service(CreateImageBytes())
            {
                FailFirstDownload = true
            };

            var reports = new RecordingProcessingReportService();
            using var services = BuildServices(options, service, reports);
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(
                ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, "folder/image.webp", out var thumbnailPath),
                Is.True);
            Assert.That(File.Exists(thumbnailPath), Is.False);
            Assert.That(File.Exists($"{thumbnailPath}.bad"), Is.False);
            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(1));

            Assert.That(manager.TryStart("admin"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(service.Downloads, Is.EqualTo(2));
            Assert.That(File.Exists(thumbnailPath), Is.True);
            Assert.That(manager.GetStatus().Report.Generated, Is.EqualTo(1));
            Assert.That(reports.Saved, Has.Count.EqualTo(2));
            Assert.That(reports.Saved.All(report => report.Type == ProcessingReportType.Thumbnail), Is.True);
            Assert.That(reports.Saved[0].Html, Does.Contain("download failed"));
            Assert.That(reports.Saved[0].Html, Does.Contain("folder/image.webp"));
        }

        [Test]
        public async Task TryStart_PreventsConcurrentRuns_AndCancelFinalizesTheReport()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            var blockingService = new BlockingThumbnailB2Service();
            using var services = BuildServices(options, blockingService);
            var manager = services.GetRequiredService<ThumbnailManager>();

            Assert.That(manager.TryStart("admin"), Is.True);
            Assert.That(manager.TryStart("admin"), Is.False);

            manager.CancelGeneration();
            blockingService.Release();
            await manager.WaitForCompletionAsync();

            var report = manager.GetStatus().Report;
            Assert.That(report.Cancelled, Is.True);
            Assert.That(report.Finished, Is.True);
            Assert.That(report.Status, Is.EqualTo("cancelled"));
        }

        private static ServiceProvider BuildServices(
            ArkaineOptions options,
            IB2Service b2,
            IProcessingReportService? reportService = null,
            IMediaConverter? converter = null)
        {
            var services = new ServiceCollection();
            var hub = new RecordingHubContext<AdminHub>();

            services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            services.AddSingleton<IHubContext<AdminHub>>(hub);
            services.AddSingleton(hub);
            services.AddSingleton<AdminJobCoordinator>();
            services.AddSingleton<IProcessingReportService>(
                reportService ?? new NoOpProcessingReportService());
            services.AddSingleton<IMediaConverter>(converter ?? new StubMediaConverter());
            services.AddScoped(_ => b2);
            services.AddLogging();
            services.AddSingleton<ThumbnailManager>();

            return services.BuildServiceProvider();
        }

        private static byte[] CreateImageBytes()
        {
            using var image = new Image<Rgba32>(20, 20);
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class ThumbnailB2Service : IB2Service
        {
            private readonly byte[] _image;
            private readonly IReadOnlyList<B2File> _files;

            public ThumbnailB2Service(
                byte[] image,
                string fileName = "folder/image.webp",
                string contentType = "image/webp")
                : this(
                    image,
                    [
                        new B2File
                        {
                            FileName = fileName,
                            ContentType = contentType,
                            Type = "upload"
                        }
                    ])
            {
            }

            public ThumbnailB2Service(byte[] image, IReadOnlyList<B2File> files)
            {
                _image = image;
                _files = files;
            }

            public bool FailFirstDownload { get; set; }
            public int Downloads { get; private set; }
            public int DownloadUrlRequests { get; private set; }

            public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
            {
                Downloads++;
                if (FailFirstDownload && Downloads == 1)
                {
                    throw new InvalidOperationException("download failed");
                }

                return Task.FromResult<Stream>(new MemoryStream(_image, writable: false));
            }

            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) =>
                Task.FromResult(new AuthResponse());

            public Task<Uri> GetDownloadUrl(string userName, string fileName, CancellationToken cancellationToken)
            {
                DownloadUrlRequests++;
                return Task.FromResult(new Uri($"https://example.invalid/file/bucket/{Uri.EscapeDataString(fileName)}?Authorization=test"));
            }

            public Task Copy(CopyRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<FilesResponse> ListFiles(
                FilesRequest request,
                string userName,
                IFavouritesService? favouritesService,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new FilesResponse
                {
                    Files = _files.ToList()
                });
            }

            public IResult Preview(string fileName) => Results.NotFound();

            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult<IResult>(Results.NotFound());

            public Task UploadMultiPartFile(
                string fileName,
                string contentType,
                Stream content,
                int chunkSize,
                CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public Task UploadSingleFile(
                string fileName,
                string contentType,
                long length,
                Stream content,
                CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        private sealed class BlockingThumbnailB2Service : IB2Service
        {
            private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) => Task.FromResult(new AuthResponse());
            public Task<Uri> GetDownloadUrl(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult(new Uri($"https://example.invalid/file/bucket/{Uri.EscapeDataString(fileName)}?Authorization=test"));
            public Task Copy(CopyRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

            public async Task<FilesResponse> ListFiles(
                FilesRequest request,
                string userName,
                IFavouritesService? favouritesService,
                CancellationToken cancellationToken)
            {
                await Task.WhenAny(_release.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                return new FilesResponse();
            }

            public IResult Preview(string fileName) => Results.NotFound();
            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) => Task.FromResult<IResult>(Results.NotFound());
            public void Release() => _release.TrySetResult();
            public Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
