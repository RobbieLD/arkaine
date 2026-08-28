using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using System.Text;

namespace Server.Arkaine.Tests
{
    public class ThumbnailManagerTests
    {
        [Test]
        public void GetStatus_ExcludesInternalConversionArtifactsFromCounts()
        {
            var root = CreateRoot();
            var options = TestOptionsFactory.Create(root);
            Directory.CreateDirectory(Path.Combine(options.THUMBNAIL_DIR, "folder"));
            Directory.CreateDirectory(Path.Combine(options.THUMBNAIL_DIR, ".conversion-temp"));
            Directory.CreateDirectory(Path.Combine(options.THUMBNAIL_DIR, ".conversion-state"));
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, "folder", "image.jpg"), Encoding.UTF8.GetBytes("jpg"));
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, "folder", "image.jpg.bad"), []);
            File.WriteAllBytes(Path.Combine(options.THUMBNAIL_DIR, ".conversion-temp", "temp.jpg"), Encoding.UTF8.GetBytes("tmp"));
            File.WriteAllText(Path.Combine(options.THUMBNAIL_DIR, ".conversion-state", "marker.json"), "{}");

            using var services = BuildServices(options, new NoOpB2Service());
            var manager = services.GetRequiredService<ThumbnailManager>();
            var status = manager.GetStatus();

            Assert.That(status.TotalThumbnails, Is.EqualTo(1));
            Assert.That(status.BadThumbnails, Is.EqualTo(1));
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

        private static ServiceProvider BuildServices(ArkaineOptions options, IB2Service b2)
        {
            var services = new ServiceCollection();
            var hub = new RecordingHubContext<AdminHub>();

            services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            services.AddSingleton<IHubContext<AdminHub>>(hub);
            services.AddSingleton(hub);
            services.AddSingleton<AdminJobCoordinator>();
            services.AddScoped(_ => b2);
            services.AddLogging();
            services.AddSingleton<ThumbnailManager>();

            return services.BuildServiceProvider();
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class BlockingThumbnailB2Service : IB2Service
        {
            private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) => Task.FromResult(new AuthResponse());

            public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, Server.Arkaine.Favourites.IFavouritesService? favouritesService, CancellationToken cancellationToken)
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
