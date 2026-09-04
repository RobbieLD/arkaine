using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Arkaine.B2;
using Server.Arkaine.Ingest;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Server.Arkaine.Tests
{
    public class B2ServiceTests
    {
        [Test]
        public async Task ListFiles_UsesReadCredentials_AndAppliesExactFilter()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            var response = await service.ListFiles(
                new FilesRequest
                {
                    ExactFileName = "gallery-alpha/image-01.jpg",
                    PageSize = 5
                },
                "reader",
                null,
                CancellationToken.None);

            Assert.That(handler.AuthKeys, Is.EquivalentTo(new[] { "read-key" }));
            Assert.That(response.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-alpha/image-01.jpg" }));
            Assert.That(response.NextFileName, Is.Empty);
        }

        [Test]
        public async Task ListFiles_RefreshesReadCredentialsAfterUnauthorizedResponse()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root)
            {
                RejectFirstReadRequest = true,
                RotateReadTokens = true
            };
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            var response = await service.ListFiles(
                new FilesRequest { PageSize = 5 },
                "reader",
                null,
                CancellationToken.None);

            Assert.That(response.Files, Has.Count.EqualTo(2));
            Assert.That(handler.AuthKeys.Count(key => key == "read-key"), Is.EqualTo(2));
        }

        [Test]
        public async Task Download_RefreshesReadCredentialsAfterUnauthorizedResponse()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root)
            {
                RejectFirstDownloadRequest = true,
                RotateReadTokens = true
            };
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            await using var response = await service.Download("reader", "source.bin", CancellationToken.None);
            using var content = new MemoryStream();
            await response.CopyToAsync(content);

            Assert.That(content.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(handler.AuthKeys.Count(key => key == "read-key"), Is.EqualTo(2));
        }

        [Test]
        public async Task GetDownloadUrl_UsesScopedAuthorizationAndEscapesTheFileName()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            var url = await service.GetDownloadUrl(
                "reader",
                "gallery alpha/video clip.mp4",
                CancellationToken.None);

            Assert.That(
                url.AbsoluteUri,
                Is.EqualTo("https://download.invalid/file/bucket/gallery%20alpha/video%20clip.mp4?Authorization=download-token"));
            Assert.That(handler.DownloadAuthorizations, Has.Count.EqualTo(1));
            Assert.That(handler.DownloadAuthorizations[0].BucketId, Is.EqualTo("bucket"));
            Assert.That(handler.DownloadAuthorizations[0].FileNamePrefix, Is.EqualTo("gallery alpha/video clip.mp4"));
            Assert.That(handler.DownloadAuthorizations[0].ValidDurationInSeconds, Is.EqualTo(900));
            Assert.That(handler.AuthKeys, Is.EqualTo(new[] { "read-key" }));
        }

        [Test]
        public async Task GetDownloadUrl_RefreshesReadCredentialsAfterUnauthorizedResponse()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root)
            {
                RejectFirstDownloadAuthorization = true,
                RotateReadTokens = true
            };
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            _ = await service.GetDownloadUrl("reader", "video.mp4", CancellationToken.None);

            Assert.That(handler.AuthKeys.Count(key => key == "read-key"), Is.EqualTo(2));
            Assert.That(handler.DownloadAuthorizations, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task UploadSingleFile_UsesWriteCredentials()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

            await service.UploadSingleFile("converted.mp4", "video/mp4", content.Length, content, CancellationToken.None);

            Assert.That(handler.AuthKeys, Is.EquivalentTo(new[] { "write-key" }));
            Assert.That(handler.RequestedWriteUploadUrl, Is.True);
            Assert.That(handler.UploadWasCalled, Is.True);
        }

        [Test]
        public async Task Delete_UsesWriteCredentials()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);

            await service.Delete(new DeleteModel { FileName = "legacy-video.avi", Id = "file-legacy" }, CancellationToken.None);

            Assert.That(handler.AuthKeys, Is.EquivalentTo(new[] { "write-key" }));
            Assert.That(handler.DeleteWasCalled, Is.True);
        }

        [Test]
        public async Task UploadMultiPartFile_IgnoresPrefixOnlyUnfinishedUpload()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            handler.UnfinishedFiles.Add(new B2File
            {
                FileName = "converted.mp4.backup",
                Id = "wrong-file",
                Size = "0"
            });
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            await using var content = new MemoryStream(
                new byte[B2MultipartLimits.MinimumPartSizeBytes + 1]);

            await service.UploadMultiPartFile(
                "converted.mp4",
                "video/mp4",
                content,
                B2MultipartLimits.MinimumPartSizeBytes,
                CancellationToken.None);

            Assert.That(handler.StartedLargeFile, Is.True);
            Assert.That(handler.FinishedLargeFile, Is.True);
        }

        [Test]
        public async Task UploadMultiPartFile_RefreshesUploadPartUrlAfterUnauthorizedResponse()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root)
            {
                RejectFirstPartUpload = true
            };
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            await using var content = new MemoryStream(
                new byte[B2MultipartLimits.MinimumPartSizeBytes + 1]);

            await service.UploadMultiPartFile(
                "converted.mp4",
                "video/mp4",
                content,
                B2MultipartLimits.MinimumPartSizeBytes,
                CancellationToken.None);

            Assert.That(handler.PartUploadUrlRequests, Is.EqualTo(2));
            Assert.That(handler.FinishedLargeFile, Is.True);
        }

        [Test]
        public void UploadMultiPartFile_RejectsSinglePartUploads()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            using var content = new MemoryStream(new byte[B2MultipartLimits.MinimumPartSizeBytes]);

            Assert.That(
                async () => await service.UploadMultiPartFile(
                    "small.mp4",
                    "video/mp4",
                    content,
                    B2MultipartLimits.MinimumPartSizeBytes,
                    CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("at least two parts"));
        }

        [Test]
        public void UploadMultiPartFile_RejectsTruncatedNonSeekableSinglePartStream()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            using var content = new NonSeekableStream(new byte[] { 1, 2, 3 });

            Assert.That(
                async () => await service.UploadMultiPartFile(
                    "truncated.mp4",
                    "video/mp4",
                    content,
                    B2MultipartLimits.MinimumPartSizeBytes,
                    CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("at least two parts"));
            Assert.That(handler.FinishedLargeFile, Is.False);
        }

        [Test]
        public void UploadMultiPartFile_RejectsMoreThanB2MaximumParts()
        {
            var root = CreateRoot();
            var handler = new RecordingB2Handler(root);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateService(handler, cache, root);
            using var content = new LengthOnlyStream(
                (long)B2MultipartLimits.MinimumPartSizeBytes * B2MultipartLimits.MaximumPartCount + 1);

            Assert.That(
                async () => await service.UploadMultiPartFile(
                    "too-large.mp4",
                    "video/mp4",
                    content,
                    B2MultipartLimits.MinimumPartSizeBytes,
                    CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("10000"));
        }

        private static B2Service CreateService(RecordingB2Handler handler, MemoryCache cache, string root)
        {
            var client = new HttpClient(handler);
            return new B2Service(
                client,
                cache,
                Options.Create(TestOptionsFactory.Create(root)),
                new RecordingHubContext<IngestHub>(),
                new EmptyTagService(),
                new ThumbnailInfoCache(),
                NullLogger<B2Service>.Instance);
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class RecordingB2Handler : HttpMessageHandler
        {
            private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

            public RecordingB2Handler(string root)
            {
                Root = root;
            }

            public string Root { get; }
            public List<string> AuthKeys { get; } = [];
            public bool RequestedWriteUploadUrl { get; private set; }
            public bool UploadWasCalled { get; private set; }
            public bool DeleteWasCalled { get; private set; }
            public bool StartedLargeFile { get; private set; }
            public bool FinishedLargeFile { get; private set; }
            public bool RejectFirstReadRequest { get; set; }
            public bool RejectFirstDownloadRequest { get; set; }
            public bool RejectFirstDownloadAuthorization { get; set; }
            public bool RotateReadTokens { get; set; }
            public bool RejectFirstPartUpload { get; set; }
            public int PartUploadUrlRequests { get; private set; }
            public List<B2File> UnfinishedFiles { get; } = [];
            public List<DownloadAuthorizationRequest> DownloadAuthorizations { get; } = [];

            private bool _readRequestRejected;
            private bool _downloadRequestRejected;
            private bool _downloadAuthorizationRejected;
            private bool _partUploadRejected;
            private int _readTokenVersion;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri?.AbsoluteUri == "https://example.invalid/auth")
                {
                    var key = DecodeBasic(request.Headers.Authorization);
                    AuthKeys.Add(key);
                    var token = key == "write-key"
                        ? "write-token"
                        : RotateReadTokens
                            ? $"read-token-{++_readTokenVersion}"
                            : "read-token";

                    return Task.FromResult(Json(HttpStatusCode.OK, new AuthResponse
                    {
                        AccountId = "account",
                        ApiBaseUrl = "https://api.invalid",
                        DownloadBaseUrl = "https://download.invalid",
                        Token = token
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_list_file_names")
                {
                    if (RejectFirstReadRequest && !_readRequestRejected)
                    {
                        _readRequestRejected = true;
                        return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "expired_auth_token" }));
                    }

                    var expectedReadToken = RotateReadTokens
                        ? $"read-token-{_readTokenVersion}"
                        : "read-token";
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo(expectedReadToken));

                    return Task.FromResult(Json(HttpStatusCode.OK, new FilesResponse
                    {
                        Files =
                        [
                            new B2File { FileName = "gallery-alpha/image-01.jpg", ContentType = "image/jpeg", Id = "1", Type = "upload", Size = "10" },
                            new B2File { FileName = "gallery-alpha/image-01.jpg.backup", ContentType = "image/jpeg", Id = "2", Type = "upload", Size = "10" }
                        ]
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://download.invalid/file/bucket/source.bin")
                {
                    if (RejectFirstDownloadRequest && !_downloadRequestRejected)
                    {
                        _downloadRequestRejected = true;
                        return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "expired_auth_token" }));
                    }

                    var expectedReadToken = RotateReadTokens
                        ? $"read-token-{_readTokenVersion}"
                        : "read-token";
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo(expectedReadToken));
                    return Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent([1, 2, 3])
                        });
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_get_download_authorization")
                {
                    if (RejectFirstDownloadAuthorization && !_downloadAuthorizationRejected)
                    {
                        _downloadAuthorizationRejected = true;
                        return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "expired_auth_token" }));
                    }

                    Assert.That(GetAuthorizationValue(request), Is.EqualTo(
                        RotateReadTokens
                            ? $"read-token-{_readTokenVersion}"
                            : "read-token"));
                    var authorization = JsonSerializer.Deserialize<DownloadAuthorizationRequest>(
                        request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                        _jsonOptions);
                    Assert.That(authorization, Is.Not.Null);
                    DownloadAuthorizations.Add(authorization!);
                    return Task.FromResult(Json(HttpStatusCode.OK, new DownloadAuthorizationResponse
                    {
                        AuthorizationToken = "download-token"
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_get_upload_url")
                {
                    RequestedWriteUploadUrl = true;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));

                    return Task.FromResult(Json(HttpStatusCode.OK, new UploadUrlResponse
                    {
                        Token = "upload-token",
                        UploadUrl = "https://upload.invalid"
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_list_unfinished_large_files")
                {
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new FilesResponse
                    {
                        Files = UnfinishedFiles
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_start_large_file")
                {
                    StartedLargeFile = true;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new StartPartUploadResponse
                    {
                        FileId = "intended-file"
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_get_upload_part_url")
                {
                    PartUploadUrlRequests++;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new GetUploadPartsResponse
                    {
                        AuthorizationToken = "part-token",
                        UploadUrl = "https://part.invalid"
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://part.invalid/")
                {
                    if (RejectFirstPartUpload && !_partUploadRejected)
                    {
                        _partUploadRejected = true;
                        return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { code = "expired_auth_token" }));
                    }

                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("part-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new { }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_finish_large_file")
                {
                    FinishedLargeFile = true;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new FinishUploadFileResponse
                    {
                        Action = "upload"
                    }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://upload.invalid/")
                {
                    UploadWasCalled = true;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("upload-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new { fileId = "uploaded" }));
                }

                if (request.RequestUri?.AbsoluteUri == "https://api.invalid/b2api/v2/b2_delete_file_version")
                {
                    DeleteWasCalled = true;
                    Assert.That(GetAuthorizationValue(request), Is.EqualTo("write-token"));
                    return Task.FromResult(Json(HttpStatusCode.OK, new DeleteModel { FileName = "legacy-video.avi", Id = "file-legacy" }));
                }

                throw new AssertionException($"Unexpected request to {request.RequestUri}");
            }

            private HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json")
                };
            }

            private static string DecodeBasic(AuthenticationHeaderValue? header)
            {
                Assert.That(header?.Scheme, Is.EqualTo("Basic"));
                Assert.That(header?.Parameter, Is.Not.Null.And.Not.Empty);
                return Encoding.UTF8.GetString(Convert.FromBase64String(header!.Parameter!));
            }

            private static string? GetAuthorizationValue(HttpRequestMessage request)
            {
                return request.Headers.TryGetValues("Authorization", out var values)
                    ? values.SingleOrDefault()
                    : request.Headers.Authorization?.Parameter;
            }
        }

        private sealed class LengthOnlyStream(long length) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => length;
            public override long Position { get; set; }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class NonSeekableStream(byte[] content) : Stream
        {
            private readonly MemoryStream _inner = new(content, writable: false);

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override int Read(Span<byte> buffer) => _inner.Read(buffer);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                _inner.ReadAsync(buffer, cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
