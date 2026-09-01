using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Tags;
using System.Text;

namespace Server.Arkaine.Tests
{
    public class ConversionManagerTests
    {
        [Test]
        public async Task ConvertAsync_OnlyProcessesFilesInSelectedPath()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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
            Assert.That(
                files,
                Does.Not.Contain("gallery-alpha/photo-01.webp"),
                string.Join(Environment.NewLine, manager.GetStatus().Report.Failures.Select(failure => failure.Error)));
            Assert.That(files, Does.Contain("gallery-beta/photo-02.webp"));
            Assert.That(files, Does.Not.Contain("gallery-beta/photo-02.jpg"));
            Assert.That(manager.GetStatus().Report.Converted, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_RootPathUsesEmptyB2Prefix()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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
        }

        [Test]
        public async Task ConvertAsync_WhenDeleteConvertedFilesIsFalse_MovesSourcesIntoConvertedFolder()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            const string source = "gallery-alpha/photo-01.webp";
            const string target = "gallery-alpha/photo-01.jpg";
            const string convertedSource = "gallery-alpha/converted/photo-01.webp";

            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")],
                options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(
                manager.TryStart("admin", "gallery-alpha/", deleteConvertedFiles: false),
                Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain(target));
            Assert.That(files, Does.Contain(convertedSource));
            Assert.That(files, Does.Not.Contain(source));
            Assert.That(manager.GetStatus().Report.DeleteConvertedFiles, Is.False);

            Assert.That(manager.TryStart("admin", "gallery-alpha/", deleteConvertedFiles: false), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(2));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(convertedSource));
        }

        [Test]
        public async Task ConvertAsync_SelectedPathDoesNotRecoverOtherPendingState()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            var bytes = Encoding.UTF8.GetBytes("image");
            const string alphaSource = "gallery-alpha/photo-01.webp";
            const string betaSource = "gallery-beta/photo-02.webp";
            const string betaTarget = "gallery-beta/photo-02.jpg";

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object(alphaSource, "image/webp", bytes, "source-alpha"),
                new MockB2.MockB2Object(betaSource, "image/webp", bytes, "source-beta")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            using var services = BuildServices(options, converter, mockStore);
            var stateStore = services.GetRequiredService<IConversionStateStore>();
            stateStore.Save(new ConversionStateMarker
            {
                SourceFile = betaSource,
                SourceId = "source-beta",
                TargetFile = betaTarget
            });

            var manager = services.GetRequiredService<ConversionManager>();
            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName),
                Does.Contain(betaSource));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName),
                Does.Not.Contain(betaTarget));
            Assert.That(stateStore.LoadAll().Select(marker => marker.SourceFile),
                Is.EqualTo(new[] { betaSource }));
        }

        [Test]
        public async Task ConvertAsync_RecoversPendingStateAndRenamesMetadataIdempotently()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            var bytes = Encoding.UTF8.GetBytes("image");
            const string source = "gallery-alpha/photo-01.webp";
            const string target = "gallery-alpha/photo-01.jpg";

            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "image/webp", bytes, "source-01")],
                options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            using var services = BuildServices(options, converter, mockStore);

            using (var scope = services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ArkaineDbContext>();
                context.Favourites.AddRange(
                    new Favourite { Name = source, UserName = "alice" },
                    new Favourite { Name = target, UserName = "alice" },
                    new Favourite { Name = source, UserName = "bob" });
                context.Tags.AddRange(
                    new Tag { FileName = source, Name = "tag-a", Timestamp = 0 },
                    new Tag { FileName = target, Name = "tag-a", Timestamp = 0 },
                    new Tag { FileName = source, Name = "tag-b", Timestamp = 15 });
                await context.SaveChangesAsync();

                var b2 = scope.ServiceProvider.GetRequiredService<IB2Service>();
                await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("converted"));
                await b2.UploadSingleFile(target, "image/jpeg", upload.Length, upload, CancellationToken.None);
            }

            var sourceFile = mockStore.SnapshotFiles().Single(file => file.FileName == source);
            var stateStore = services.GetRequiredService<IConversionStateStore>();
            stateStore.Save(new ConversionStateMarker
            {
                SourceFile = source,
                SourceId = sourceFile.Id,
                TargetFile = target
            });

            Assert.That(ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, source, out var sourceThumbnail), Is.True);
            Assert.That(File.Exists(sourceThumbnail), Is.True);

            var manager = services.GetRequiredService<ConversionManager>();
            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain(target));
            Assert.That(
                files,
                Does.Not.Contain(source),
                string.Join(Environment.NewLine, manager.GetStatus().Report.Failures.Select(failure => failure.Error)));
            Assert.That(converter.Requests, Is.Empty);

            Assert.That(ThumbnailPathResolver.TryResolve(options.THUMBNAIL_DIR, target, out var targetThumbnail), Is.True);
            Assert.That(File.Exists(targetThumbnail), Is.True);
            Assert.That(File.Exists(sourceThumbnail), Is.False);
            Assert.That(stateStore.LoadAll(), Is.Empty);

            using var verificationScope = services.CreateScope();
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ArkaineDbContext>();
            var favourites = await verificationContext.Favourites.OrderBy(favourite => favourite.UserName).ToListAsync();
            var tags = await verificationContext.Tags.OrderBy(tag => tag.Name).ToListAsync();

            Assert.That(favourites.Select(favourite => (favourite.UserName, favourite.Name)).ToArray(),
                Is.EqualTo(new[] { ("alice", target), ("bob", target) }));
            Assert.That(tags.Select(tag => (tag.Name, tag.FileName, tag.Timestamp)).ToArray(),
                Is.EqualTo(new[] { ("tag-a", target, 0), ("tag-b", target, 15) }));
            Assert.That(manager.GetStatus().Report.Recovered, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_SkipsExistingTargetAndLeavesSourceUntouched()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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
        public async Task ConvertAsync_WritesPermanentFailureMarkerAndSkipsItOnRetry()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            const string source = "gallery-alpha/photo-01.webp";

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter
            {
                OnConvertAsync = (_, _) => Task.FromResult(
                    new Server.Arkaine.Media.MediaConversionResult(
                        false,
                        1,
                        "unsupported input",
                        TimeSpan.Zero,
                        false,
                        false))
            };
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(ThumbnailPathResolver.TryResolve(
                options.THUMBNAIL_DIR,
                $"{source}.convert-failed",
                out var failureMarker), Is.True);
            Assert.That(File.Exists(failureMarker), Is.True);
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(source));
            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(1));

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Has.Count.EqualTo(1));
            Assert.That(manager.GetStatus().Report.Skipped, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_TimeoutRetainsPendingStateWithoutPermanentMarker()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            const string source = "gallery-alpha/photo-01.webp";

            var mockStore = CreateMockStore(
            [
                new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")
            ], options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter
            {
                OnConvertAsync = (_, _) => Task.FromResult(
                    new Server.Arkaine.Media.MediaConversionResult(
                        false,
                        -1,
                        "conversion timed out",
                        TimeSpan.FromMinutes(5),
                        true,
                        false))
            };
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(ThumbnailPathResolver.TryResolve(
                options.THUMBNAIL_DIR,
                $"{source}.convert-failed",
                out var failureMarker), Is.True);
            Assert.That(File.Exists(failureMarker), Is.False);
            Assert.That(services.GetRequiredService<IConversionStateStore>().LoadAll(), Has.Count.EqualTo(1));
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(source));
        }

        [Test]
        public async Task ConvertAsync_SkipsUnsupportedKeysWithoutNormalizingOrDeletingThem()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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

        [TestCase("gallery-alpha/bad:name.mov")]
        [TestCase("gallery-alpha/folder. /clip.mov")]
        [TestCase("gallery-alpha/folder\\clip.mov")]
        [TestCase("gallery-alpha/folder//clip.mov")]
        public async Task ConvertAsync_RejectsConvertibleKeysThatCannotMapSafelyToThumbnails(string source)
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "video/quicktime", Encoding.UTF8.GetBytes("video"), "source-01")],
                options.THUMBNAIL_DIR);

            var converter = new StubMediaConverter();
            using var services = BuildServices(options, converter, mockStore);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            Assert.That(converter.Requests, Is.Empty);
            Assert.That(mockStore.SnapshotFiles().Select(file => file.FileName), Does.Contain(source));
            Assert.That(services.GetRequiredService<IConversionStateStore>().LoadAll(), Is.Empty);
            Assert.That(manager.GetStatus().Report.Failed, Is.EqualTo(1));
        }

        [Test]
        public async Task ConvertAsync_DoesNotDeleteUnrelatedDirectoriesInTempRoot()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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
        public async Task ConvertAsync_DoesNotDeleteSourceWhenUploadedTargetCannotBeVerified()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
            const string source = "gallery-alpha/photo-01.webp";
            const string target = "gallery-alpha/photo-01.jpg";
            var mockStore = CreateMockStore(
                [new MockB2.MockB2Object(source, "image/webp", Encoding.UTF8.GetBytes("image"), "source-01")],
                options.THUMBNAIL_DIR);
            var b2 = new DroppingUploadB2Service(mockStore);

            using var services = BuildServices(options, new StubMediaConverter(), mockStore, b2);
            var manager = services.GetRequiredService<ConversionManager>();

            Assert.That(manager.TryStart("admin", "gallery-alpha/"), Is.True);
            await manager.WaitForCompletionAsync();

            var files = mockStore.SnapshotFiles().Select(file => file.FileName).ToArray();
            Assert.That(files, Does.Contain(source));
            Assert.That(files, Does.Not.Contain(target));
            Assert.That(b2.DeleteCalled, Is.False);
            Assert.That(services.GetRequiredService<IConversionStateStore>().LoadAll(), Has.Count.EqualTo(1));
            Assert.That(
                manager.GetStatus().Report.Failures.Select(failure => failure.Error),
                Has.Some.Contains("could not be verified"));
        }

        [Test]
        public async Task TryStart_PreventsConcurrentRuns_AndCancelMarksTheReport()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(options.THUMBNAIL_DIR);
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
            var databaseName = Guid.NewGuid().ToString("n");

            services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            services.AddSingleton<IHubContext<AdminHub>>(hub);
            services.AddSingleton(hub);
            services.AddSingleton<IThumbnailInfoProvider, ThumbnailInfoCache>();
            services.AddSingleton<AdminJobCoordinator>();
            services.AddSingleton<IConversionStateStore, FileSystemConversionStateStore>();
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
            services.AddScoped<IMediaLibraryReferenceService, MediaLibraryReferenceService>();
            services.AddDbContext<ArkaineDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
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

        private sealed class DroppingUploadB2Service : IB2Service
        {
            private readonly MockB2 _inner;

            public DroppingUploadB2Service(MockB2.Store store)
            {
                _inner = new MockB2(new ThumbnailInfoCache(), store);
            }

            public bool DeleteCalled { get; private set; }

            public Task Delete(DeleteModel request, CancellationToken cancellationToken)
            {
                DeleteCalled = true;
                return _inner.Delete(request, cancellationToken);
            }

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
                CancellationToken cancellationToken) =>
                _inner.ListFiles(request, userName, favouritesService, cancellationToken);

            public IResult Preview(string fileName) => _inner.Preview(fileName);

            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) =>
                _inner.Stream(userName, fileName, cancellationToken);

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
    }
}
