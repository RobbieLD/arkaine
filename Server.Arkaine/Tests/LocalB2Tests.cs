using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Arkaine.B2;
using Server.Arkaine.Ingest;
using Server.Arkaine.LocalB2;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Server.Arkaine.Tests;

public class LocalB2Tests
{
    [Test]
    public async Task ListFiles_HonorsPrefixDelimiterAndPaging()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(Path.Combine(root, "gallery-alpha", "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "gallery-alpha", "photo.webp"), "photo");
        await File.WriteAllTextAsync(Path.Combine(root, "gallery-alpha", "nested", "clip.mov"), "clip");
        await File.WriteAllTextAsync(Path.Combine(root, "gallery-beta.jpg"), "beta");

        await using var app = await CreateAppAsync(root);
        using var client = app.GetTestClient();

        var rootPage = await PostListAsync(client, new
        {
            bucketId = "local-bucket",
            delimiter = "/",
            maxFileCount = 1
        });
        var nextPage = await PostListAsync(client, new
        {
            bucketId = "local-bucket",
            delimiter = "/",
            maxFileCount = 1,
            startFileName = rootPage.NextFileName
        });
        var folderPage = await PostListAsync(client, new
        {
            bucketId = "local-bucket",
            delimiter = "/",
            prefix = "gallery-alpha/",
            maxFileCount = 10
        });

        Assert.That(rootPage.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-alpha/" }));
        Assert.That(rootPage.NextFileName, Is.EqualTo("gallery-alpha/"));
        Assert.That(nextPage.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-beta.jpg" }));
        Assert.That(folderPage.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-alpha/nested/", "gallery-alpha/photo.webp" }));
        Assert.That(folderPage.Files.Single(file => file.FileName == "gallery-alpha/photo.webp").ContentType, Is.EqualTo("image/webp"));
    }

    [Test]
    public async Task B2Service_RoundTripsUploadCopyDownloadAndDelete()
    {
        var root = CreateRoot();
        await using var app = await CreateAppAsync(root);
        using var client = app.GetTestClient();
        var service = CreateB2Service(client, root);
        var payload = Encoding.UTF8.GetBytes("local b2 payload");

        await using (var content = new MemoryStream(payload))
        {
            await service.UploadSingleFile(
                "gallery-alpha/photo.webp",
                "image/webp",
                payload.Length,
                content,
                CancellationToken.None);
        }

        var listed = await service.ListFiles(
            new FilesRequest { Prefix = "gallery-alpha/", PageSize = 10 },
            "test-user",
            null,
            CancellationToken.None);
        var source = listed.Files.Single();

        await service.Copy(
            new CopyRequest
            {
                Id = source.Id,
                FileName = "gallery-alpha/converted/photo.webp"
            },
            CancellationToken.None);

        using (var downloaded = await service.Download(
                   "test-user",
                   "gallery-alpha/converted/photo.webp",
                   CancellationToken.None))
        using (var buffer = new MemoryStream())
        {
            await downloaded.CopyToAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EqualTo(payload));
        }

        using (var rangeRequest = new HttpRequestMessage(
                   HttpMethod.Get,
                   "/file/local/gallery-alpha/converted/photo.webp"))
        {
            rangeRequest.Headers.Range = new RangeHeaderValue(6, 9);
            var rangeResponse = await client.SendAsync(rangeRequest);

            Assert.That(rangeResponse.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
            Assert.That(await rangeResponse.Content.ReadAsByteArrayAsync(), Is.EqualTo(payload[6..10]));
        }

        var downloadUrl = await service.GetDownloadUrl(
            "test-user",
            "gallery-alpha/converted/photo.webp",
            CancellationToken.None);
        using (var remoteResponse = await client.GetAsync(downloadUrl))
        {
            remoteResponse.EnsureSuccessStatusCode();
            Assert.That(await remoteResponse.Content.ReadAsByteArrayAsync(), Is.EqualTo(payload));
        }

        await service.Delete(
            new DeleteModel
            {
                FileName = source.FileName,
                Id = source.Id
            },
            CancellationToken.None);

        var remaining = await service.ListFiles(
            new FilesRequest { Prefix = "gallery-alpha/", PageSize = 10 },
            "test-user",
            null,
            CancellationToken.None);
        Assert.That(remaining.Files.Select(file => file.FileName), Is.EqualTo(new[] { "gallery-alpha/converted/photo.webp" }));
    }

    [Test]
    public async Task B2Service_RoundTripsMultipartUpload()
    {
        var root = CreateRoot();
        await using var app = await CreateAppAsync(root);
        using var client = app.GetTestClient();
        var service = CreateB2Service(client, root);
        var payload = new byte[B2MultipartLimits.MinimumPartSizeBytes * 2];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % byte.MaxValue);
        }

        await using (var content = new MemoryStream(payload))
        {
            await service.UploadMultiPartFile(
                "gallery-alpha/multipart.bin",
                "application/custom",
                content,
                B2MultipartLimits.MinimumPartSizeBytes,
                CancellationToken.None);
        }

        var listed = await service.ListFiles(
            new FilesRequest { Prefix = "gallery-alpha/", PageSize = 10 },
            "test-user",
            null,
            CancellationToken.None);
        var file = listed.Files.Single(file => file.FileName == "gallery-alpha/multipart.bin");
        Assert.That(file.ContentType, Is.EqualTo("application/custom"));
        Assert.That(file.Size, Is.EqualTo("9 MB"));

        await using var downloaded = await service.Download(
            "test-user",
            file.FileName,
            CancellationToken.None);
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);
        Assert.That(SHA1.HashData(buffer.ToArray()), Is.EqualTo(SHA1.HashData(payload)));
    }

    [Test]
    public async Task B2Service_ConnectsToLocalEmulatorThroughConfiguredPrivateHost()
    {
        var root = CreateRoot();
        await using var app = await CreateKestrelAppAsync(root);
        var baseAddress = new Uri(app.Urls.Single());
        using var client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = (context, cancellationToken) =>
                UrlSafetyValidator.ConnectAsync(context, cancellationToken, baseAddress.DnsSafeHost)
        });
        var service = CreateB2Service(
            client,
            root,
            $"{baseAddress}b2api/v2/b2_authorize_account");

        var response = await service.ListFiles(
            new FilesRequest { PageSize = 10 },
            "test-user",
            null,
            CancellationToken.None);

        Assert.That(response.Files, Is.Empty);
    }

    [Test]
    public async Task MultipartEndpoints_AssemblePartsAndPreserveContentType()
    {
        var root = CreateRoot();
        await using var app = await CreateAppAsync(root);
        using var client = app.GetTestClient();

        var start = await client.PostAsJsonAsync(
            "/b2api/v2/b2_start_large_file",
            new
            {
                bucketId = "local-bucket",
                fileName = "gallery-alpha/large.bin",
                contentType = "application/custom"
            });
        var startBody = await start.Content.ReadFromJsonAsync<MultipartStartResponse>();
        Assert.That(startBody, Is.Not.Null);

        var first = Encoding.UTF8.GetBytes("first-");
        var second = Encoding.UTF8.GetBytes("second");
        await UploadPartAsync(client, startBody!.FileId, 1, first);
        await UploadPartAsync(client, startBody.FileId, 2, second);
        var hashes = new[]
        {
            Convert.ToHexStringLower(SHA1.HashData(first)),
            Convert.ToHexStringLower(SHA1.HashData(second))
        };

        var finish = await client.PostAsJsonAsync(
            "/b2api/v2/b2_finish_large_file",
            new
            {
                fileId = startBody.FileId,
                partSha1Array = hashes
            });
        finish.EnsureSuccessStatusCode();

        var file = await client.GetAsync("/file/local/gallery-alpha/large.bin");
        Assert.That(file.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(file.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/custom"));
        Assert.That(await file.Content.ReadAsStringAsync(), Is.EqualTo("first-second"));
    }

    [TestCase("../outside.txt")]
    [TestCase("/outside.txt")]
    [TestCase("gallery-alpha/../outside.txt")]
    [TestCase("gallery-alpha\\outside.txt")]
    [TestCase(".local-b2/metadata.json")]
    public async Task FileOperations_RejectUnsafeKeys(string key)
    {
        var root = CreateRoot();
        await using var app = await CreateAppAsync(root);
        using var client = app.GetTestClient();

        var uploadUrl = await client.PostAsJsonAsync(
            "/b2api/v2/b2_get_upload_url",
            new { bucketId = "local-bucket" });
        var uploadBody = await uploadUrl.Content.ReadFromJsonAsync<UploadUrlResponse>();
        Assert.That(uploadBody, Is.Not.Null);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadBody!.UploadUrl)
        {
            Content = new ByteArrayContent([1, 2, 3])
        };
        request.Content.Headers.ContentType = new("application/octet-stream");
        request.Headers.TryAddWithoutValidation("X-Bz-File-Name", Uri.EscapeDataString(key));

        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private static async Task UploadPartAsync(HttpClient client, string fileId, int partNumber, byte[] content)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/b2api/v2/b2_upload_part?fileId={Uri.EscapeDataString(fileId)}")
        {
            Content = new ByteArrayContent(content)
        };
        request.Headers.TryAddWithoutValidation("X-Bz-Part-Number", partNumber.ToString());
        request.Headers.TryAddWithoutValidation(
            "X-Bz-Content-Sha1",
            Convert.ToHexStringLower(SHA1.HashData(content)));

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<LocalListResponse> PostListAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/b2api/v2/b2_list_file_names", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LocalListResponse>())!;
    }

    private static B2Service CreateB2Service(HttpClient client, string root, string? authUrl = null)
    {
        var options = TestOptionsFactory.Create(root);
        options.B2AuthUrl = authUrl ?? "http://localhost/b2api/v2/b2_authorize_account";
        options.B2_KEY_READ = "local-read";
        options.B2_KEY_WRITE = "local-write";
        options.BUCKET_ID = "local-bucket";
        options.BUCKET_NAME = "local";
        return new B2Service(
            client,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(options),
            new RecordingHubContext<IngestHub>(),
            new EmptyTagService(),
            new ThumbnailInfoCache(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<B2Service>.Instance);
    }

    private static async Task<WebApplication> CreateAppAsync(string root)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new LocalB2Options
        {
            RootDirectory = root,
            BucketId = "local-bucket",
            BucketName = "local"
        };
        options.Normalize();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<LocalB2Store>();
        var app = builder.Build();
        app.MapLocalB2();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> CreateKestrelAppAsync(string root)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var options = new LocalB2Options
        {
            RootDirectory = root,
            BucketId = "local-bucket",
            BucketName = "local"
        };
        options.Normalize();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<LocalB2Store>();
        var app = builder.Build();
        app.MapLocalB2();
        await app.StartAsync();
        return app;
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "artifacts",
            Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class LocalListResponse
    {
        public List<LocalListFile> Files { get; set; } = [];
        public string NextFileName { get; set; } = string.Empty;
    }

    private sealed class LocalListFile
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    private sealed class MultipartStartResponse
    {
        public string FileId { get; set; } = string.Empty;
    }
}
