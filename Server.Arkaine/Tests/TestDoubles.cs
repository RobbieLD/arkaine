using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Server.Arkaine.Admin;
using Server.Arkaine.B2;
using Server.Arkaine.Favourites;
using Server.Arkaine.Media;
using Server.Arkaine.Tags;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Server.Arkaine.Tests
{
    internal sealed class RecordingHubContext<THub> : IHubContext<THub> where THub : Hub
    {
        public RecordingHubContext()
        {
            Clients = new RecordingHubClients();
            Groups = new RecordingGroupManager();
        }

        public RecordingHubClients TypedClients => (RecordingHubClients)Clients;
        public IHubClients Clients { get; }
        public IGroupManager Groups { get; }
    }

    internal sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingClientProxy _proxy = new();

        public IReadOnlyList<(string Method, object?[] Args)> Messages => _proxy.Messages;

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    internal sealed class RecordingClientProxy : IClientProxy
    {
        private readonly List<(string Method, object?[] Args)> _messages = [];

        public IReadOnlyList<(string Method, object?[] Args)> Messages => _messages;

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            _messages.Add((method, args));
            return Task.CompletedTask;
        }
    }

    internal sealed class RecordingGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class QueueProcessRunner : IProcessRunner
    {
        private readonly Queue<Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<ProcessRunResult>>> _responses = new();

        public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Calls { get; } = [];

        public void Enqueue(ProcessRunResult result)
        {
            _responses.Enqueue((_, _, _) => Task.FromResult(result));
        }

        public void Enqueue(Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<ProcessRunResult>> response)
        {
            _responses.Enqueue(response);
        }

        public Task<ProcessRunResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls.Add((startInfo.FileName, startInfo.ArgumentList.ToArray(), timeout));
            return _responses.Count == 0
                ? Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false))
                : _responses.Dequeue()(startInfo, timeout, cancellationToken);
        }
    }

    internal sealed class StubMediaConverter : IMediaConverter
    {
        public MediaConverterAvailability Availability { get; set; } =
            new(true, true, true, "ffmpeg", "ffmpeg version test", [], string.Empty);

        public List<MediaConversionRequest> Requests { get; } = [];

        public Func<MediaConversionRequest, CancellationToken, Task<MediaConversionResult>> OnConvertAsync { get; set; } =
            async (request, cancellationToken) =>
            {
                await File.WriteAllTextAsync(request.TargetPath, "converted", cancellationToken);
                return new MediaConversionResult(true, 0, string.Empty, TimeSpan.Zero, false, false);
            };

        public Task<MediaConverterAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Availability);
        }

        public Task<MediaConversionResult> ConvertAsync(MediaConversionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return OnConvertAsync(request, cancellationToken);
        }
    }

    internal sealed class EmptyTagService : ITagService
    {
        public Task<IEnumerable<Tag>> AddTag(AddTagRequest request) => Task.FromResult<IEnumerable<Tag>>([]);
        public Task<IEnumerable<Tag>> DeleteTag(int id) => Task.FromResult<IEnumerable<Tag>>([]);
        public Task<IEnumerable<string>> GetFileNamesForTag(string name) => Task.FromResult<IEnumerable<string>>([]);
        public Task<IDictionary<string, IEnumerable<Tag>>> GetTagsForFile(IEnumerable<string> files) =>
            Task.FromResult<IDictionary<string, IEnumerable<Tag>>>(new Dictionary<string, IEnumerable<Tag>>());
    }

    internal sealed class NoOpProcessingReportService : IProcessingReportService
    {
        public Task SaveAsync(
            ProcessingReportType type,
            DateTimeOffset createdUtc,
            string html,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ProcessingReportSummary>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessingReportSummary>>([]);

        public Task<StoredProcessingReport?> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<StoredProcessingReport?>(null);

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class RecordingProcessingReportService : IProcessingReportService
    {
        public List<(ProcessingReportType Type, DateTimeOffset CreatedUtc, string Html)> Saved { get; } = [];

        public Task SaveAsync(
            ProcessingReportType type,
            DateTimeOffset createdUtc,
            string html,
            CancellationToken cancellationToken)
        {
            Saved.Add((type, createdUtc, html));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProcessingReportSummary>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessingReportSummary>>(
                Saved.Select((report, index) => new ProcessingReportSummary(
                    index + 1,
                    report.Type == ProcessingReportType.Thumbnail ? "thumbnail" : "conversion",
                    report.CreatedUtc.ToString("O"),
                    report.CreatedUtc)).ToList());

        public Task<StoredProcessingReport?> GetAsync(int id, CancellationToken cancellationToken)
        {
            if (id <= 0 || id > Saved.Count)
            {
                return Task.FromResult<StoredProcessingReport?>(null);
            }

            var report = Saved[id - 1];
            return Task.FromResult<StoredProcessingReport?>(
                new StoredProcessingReport(
                    id,
                    report.Type == ProcessingReportType.Thumbnail ? "thumbnail" : "conversion",
                    report.CreatedUtc.ToString("O"),
                    report.CreatedUtc,
                    report.Html));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Saved.Clear();
            return Task.CompletedTask;
        }
    }

    internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    internal sealed class TestUserAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestUserAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "user"),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    internal static class TestOptionsFactory
    {
        public static ArkaineOptions Create(string rootDirectory)
        {
            var options = new ArkaineOptions
            {
                BUCKET_ID = "bucket",
                BUCKET_NAME = "bucket",
                B2AuthUrl = "https://example.invalid/auth",
                B2_KEY_READ = "read-key",
                B2_KEY_WRITE = "write-key",
                PAGE_SIZE = "25",
                THUMBNAIL_DIR = Path.Combine(rootDirectory, "thumbnails"),
                THUMBNAIL_EXTENSIONS = ".jpg,.jpeg,.png,.webp",
                THUMBNAIL_PAGE_SIZE = 25,
                THUMBNAIL_WIDTH = 350,
                UPLOAD_CHUNK_SIZE = B2MultipartLimits.MinimumPartSizeBytes,
                CONVERSION_PAGE_SIZE = 25,
                IMAGE_EXTENSIONS = ".webp",
                VIDEO_EXTENSIONS = ".avi,.mov",
                CONVERSION_TEMP_DIR = Path.Combine(rootDirectory, "conversion-temp"),
                FFMPEG_PATH = "ffmpeg",
                FFMPEG_CONVERSION_TIMEOUT_SECONDS = 30,
                FFMPEG_PROBE_TIMEOUT_SECONDS = 5
            };
            options.Normalize();
            return options;
        }
    }

    internal sealed class NoOpB2Service : IB2Service
    {
        public Task Delete(DeleteModel request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken) => Task.FromResult(new AuthResponse());
        public Task Copy(CopyRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken) => Task.FromResult(new FilesResponse());
        public IResult Preview(string fileName) => Results.NotFound();
        public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken) => Task.FromResult<IResult>(Results.NotFound());
        public Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
