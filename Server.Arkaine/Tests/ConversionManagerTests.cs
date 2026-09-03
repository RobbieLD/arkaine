using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
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
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain(source));
            Assert.That(files, Does.Contain(target));
            Assert.That(converter.Requests, Is.Empty);
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(2));
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
            IB2Service? b2Override = null)
        {
            var services = new ServiceCollection();
            var hub = new RecordingHubContext<AdminHub>();

            services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            services.AddSingleton<IHubContext<AdminHub>>(hub);
            services.AddSingleton(hub);
            services.AddSingleton<IThumbnailInfoProvider, ThumbnailInfoCache>();
            services.AddSingleton<AdminJobCoordinator>();
            services.AddSingleton<Server.Arkaine.Media.IMediaConverter>(converter);
            services.AddSingleton(mockStore);
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
