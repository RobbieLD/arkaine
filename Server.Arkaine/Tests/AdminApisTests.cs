using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Media;
using System.Net.Http.Json;

namespace Server.Arkaine.Tests
{
    public class AdminApisTests
    {
        [Test]
        public async Task StatusRoute_ReturnsCanonicalStatus_AndSettingsAliasStillWorks()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter
            {
                Availability = new MediaConverterAvailability(true, true, true, "ffmpeg", "ffmpeg 7.0", [], string.Empty)
            });

            using (app)
            {
                var client = app.GetTestClient();

                var status = await client.GetFromJsonAsync<AdminStatusResponse>("/admin/status");
                var admin = await client.GetFromJsonAsync<AdminStatusResponse>("/admin");
                var settings = await client.GetFromJsonAsync<SettingsResponse>("/settings");
                var cache = await client.GetFromJsonAsync<ThumbnailCacheStats>("/admin/thumbnail-cache");

                Assert.That(status, Is.Not.Null);
                Assert.That(admin, Is.Not.Null);
                Assert.That(status!.Ffmpeg.IsAvailable, Is.True);
                Assert.That(status.Thumbnail.ThumbnailPageSize, Is.EqualTo(25));
                Assert.That(status.Conversion.ConversionPageSize, Is.EqualTo(25));
                Assert.That(cache, Is.Not.Null);
                Assert.That(settings, Is.Not.Null);
                Assert.That(settings!.ThumbnailWidth, Is.EqualTo(350));
            }
        }

        [Test]
        public async Task StartRoute_StartsConversionJob_UsingCanonicalEndpoint()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter());

            using (app)
            {
                var client = app.GetTestClient();
                var response = await client.PostAsJsonAsync("/admin/start", new AdminJobRequest
                {
                    Job = "conversion",
                    Path = "gallery-alpha"
                });

                response.EnsureSuccessStatusCode();
                var status = await response.Content.ReadFromJsonAsync<AdminStatusResponse>();

                Assert.That(status, Is.Not.Null);
                Assert.That(status!.Conversion.Report.Path, Is.EqualTo("gallery-alpha/"));
            }
        }

        [Test]
        public async Task ConversionPathsRoute_ReturnsTopLevelFoldersAcrossPages()
        {
            var root = CreateRoot();
            var b2 = new FolderListingB2Service();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter(), b2: b2);

            using (app)
            {
                var paths = await app.GetTestClient().GetFromJsonAsync<string[]>("/admin/conversion/paths");

                Assert.That(paths, Is.EqualTo(new[] { "gallery-alpha/", "gallery-beta/" }));
                Assert.That(b2.Requests, Has.Count.EqualTo(2));
                Assert.That(b2.Requests[0].Delimiter, Is.EqualTo("/"));
                Assert.That(b2.Requests[1].StartFile, Is.EqualTo("gallery-beta/"));
            }
        }

        [TestCase("")]
        [TestCase("gallery-alpha/photo-01.webp")]
        [TestCase("/gallery-alpha")]
        [TestCase("..")]
        public async Task ConversionStart_RejectsInvalidPath(string path)
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter());

            using (app)
            {
                var response = await app.GetTestClient().PostAsJsonAsync(
                    "/admin/convert/start",
                    new AdminJobRequest { Path = path });

                Assert.That((int)response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            }
        }

        [Test]
        public async Task ConversionStart_ReturnsUnavailableWhenFfmpegIsMissing()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter
            {
                Availability = new MediaConverterAvailability(
                    false,
                    false,
                    false,
                    "ffmpeg",
                    string.Empty,
                    ["libx264", "aac"],
                    "required encoders are missing")
            });

            using (app)
            {
                var response = await app.GetTestClient().PostAsJsonAsync(
                    "/admin/convert/start",
                    new AdminJobRequest { Path = "gallery-alpha/" });

                Assert.That((int)response.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            }
        }

        [Test]
        public async Task AdminMutationRoutes_ArePostOnly()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter());

            using (app)
            {
                var response = await app.GetTestClient().GetAsync("/admin/thumbnails/start");

                Assert.That((int)response.StatusCode, Is.EqualTo(StatusCodes.Status405MethodNotAllowed));
            }
        }

        [Test]
        public async Task AdminRoutes_RejectAuthenticatedNonAdminUsers()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter(), isAdmin: false);

            using (app)
            {
                var response = await app.GetTestClient().GetAsync("/admin");

                Assert.That((int)response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            }
        }

        [Test]
        public async Task CacheAndStopRoutes_UseCanonicalAdminEndpoints()
        {
            var root = CreateRoot();
            var app = await CreateAppAsync(TestOptionsFactory.Create(root), new StubMediaConverter());

            using (app)
            {
                var client = app.GetTestClient();

                var cache = await client.GetFromJsonAsync<ThumbnailCacheStats>("/admin/cache");
                Assert.That(cache, Is.Not.Null);

                var cleared = await client.PostAsync("/admin/cache/clear", null);
                cleared.EnsureSuccessStatusCode();

                var stop = await client.PostAsJsonAsync("/admin/stop", new AdminJobRequest { Job = "conversion" });
                stop.EnsureSuccessStatusCode();
                var status = await stop.Content.ReadFromJsonAsync<AdminStatusResponse>();

                Assert.That(status, Is.Not.Null);
                Assert.That(status!.Conversion.Report.Running, Is.False);
            }
        }

        [Test]
        public async Task StartRoutes_PreventThumbnailAndConversionJobsRunningTogether()
        {
            var root = CreateRoot();
            var blockingB2 = new BlockingAdminB2Service();
            var app = await CreateAppAsync(
                TestOptionsFactory.Create(root),
                new StubMediaConverter(),
                b2: blockingB2);

            using (app)
            {
                var client = app.GetTestClient();
                var thumbnailStart = await client.PostAsync("/admin/thumbnails/start", null);
                thumbnailStart.EnsureSuccessStatusCode();

                var conversionStart = await client.PostAsJsonAsync(
                    "/admin/convert/start",
                    new AdminJobRequest { Path = "gallery-alpha/" });
                Assert.That((int)conversionStart.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

                await client.PostAsync("/admin/thumbnails/stop", null);
                await app.Services.GetRequiredService<ThumbnailManager>().WaitForCompletionAsync();
            }
        }

        private static async Task<WebApplication> CreateAppAsync(
            ArkaineOptions options,
            StubMediaConverter converter,
            bool isAdmin = true,
            IB2Service? b2 = null)
        {
            Directory.CreateDirectory(options.THUMBNAIL_DIR);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);
            if (isAdmin)
            {
                authentication.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    _ => { });
            }
            else
            {
                authentication.AddScheme<AuthenticationSchemeOptions, TestUserAuthHandler>(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    _ => { });
            }
            builder.Services.AddAuthorization();
            builder.Services.AddSignalR();
            builder.Services.AddLogging();
            builder.Services.AddSingleton<IOptions<ArkaineOptions>>(Options.Create(options));
            builder.Services.AddSingleton<IThumbnailInfoProvider, ThumbnailInfoCache>();
            builder.Services.AddSingleton<IConversionStateStore, FileSystemConversionStateStore>();
            builder.Services.AddSingleton<IMediaConverter>(converter);
            builder.Services.AddSingleton<IHubContext<AdminHub>>(new RecordingHubContext<AdminHub>());
            builder.Services.AddSingleton<AdminJobCoordinator>();
            builder.Services.AddSingleton<ThumbnailManager>();
            builder.Services.AddSingleton<ConversionManager>();
            builder.Services.AddScoped(_ => b2 ?? new NoOpB2Service());
            builder.Services.AddScoped<IMediaLibraryReferenceService, NoOpReferenceService>();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.RegisterAdminApis();
            await app.StartAsync();
            return app;
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class NoOpReferenceService : IMediaLibraryReferenceService
        {
            public Task RenameFileReferencesAsync(string sourceFile, string targetFile, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class BlockingAdminB2Service : IB2Service
        {
            public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult<Stream>(new MemoryStream());
            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) =>
                Task.FromResult(new AuthResponse());

            public async Task<FilesResponse> ListFiles(
                FilesRequest request,
                string userName,
                IFavouritesService? favouritesService,
                CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new FilesResponse();
            }

            public IResult Preview(string fileName) => Results.NotFound();
            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult<IResult>(Results.NotFound());
            public Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        private sealed class FolderListingB2Service : IB2Service
        {
            public List<FilesRequest> Requests { get; } = [];

            public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult<Stream>(new MemoryStream());
            public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) =>
                Task.FromResult(new AuthResponse());

            public Task<FilesResponse> ListFiles(
                FilesRequest request,
                string userName,
                IFavouritesService? favouritesService,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);

                return Task.FromResult(request.StartFile is null
                    ? new FilesResponse
                    {
                        Files =
                        [
                            new B2File { FileName = "gallery-beta/", Type = "folder" },
                            new B2File { FileName = "root.jpg", Type = "upload" }
                        ],
                        NextFileName = "gallery-beta/"
                    }
                    : new FilesResponse
                    {
                        Files =
                        [
                            new B2File { FileName = "gallery-alpha/", Type = "folder" },
                            new B2File { FileName = "nested/path/", Type = "folder" }
                        ]
                    });
            }

            public IResult Preview(string fileName) => Results.NotFound();
            public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) =>
                Task.FromResult<IResult>(Results.NotFound());
            public Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }
    }
}
