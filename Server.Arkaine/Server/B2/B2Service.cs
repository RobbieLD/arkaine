using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.Favourites;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.SignalR;
using Server.Arkaine.Ingest;
using System.Security.Cryptography;
using Server.Arkaine.Tags;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Web;

namespace Server.Arkaine.B2
{
    public class B2Service : IB2Service
    {
        private const string WriteCacheKey = "__b2_write__";

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IHubContext<IngestHub> _hubContext;
        private readonly ArkaineOptions _options;
        private readonly ITagService _tagService;
        private readonly IThumbnailInfoProvider _thumbnails;

        public B2Service(
            HttpClient httpClient,
            IMemoryCache cache,
            IOptions<ArkaineOptions> config,
            IHubContext<IngestHub> hubContext,
            ITagService tagService,
            IThumbnailInfoProvider thumbnails,
            ILogger<B2Service> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _options = config.Value;
            _options.Normalize();
            _hubContext = hubContext;
            _tagService = tagService;
            _thumbnails = thumbnails;
        }

        public async Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken)
        {
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.GetAsync(_options.B2AuthUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Auth API call responded with: {response.StatusCode}");
                throw new HttpRequestException($"Auth API call failed with status code {response.StatusCode}.");
            }

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<AuthResponse>(responseString) ?? throw new("Response not in the correct form");

            _logger.LogInformation("Get token succeeded");

            await _hubContext.Clients.All.SendAsync("update", "Get token succeeded", cancellationToken);
            
            return responseModel;
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken)
        {
            // Fill in config options in the request
            request.BucketId = _options.BUCKET_ID;
            request.PageSize = request.PageSize > 0 ? request.PageSize : _options.GetPageSize();

            if (!string.IsNullOrWhiteSpace(request.ExactFileName))
            {
                request.Prefix = request.ExactFileName;
                request.StartFile = null;
            }

            // This is a special case of a pseudo collection
            if (favouritesService != null && (request.Prefix?.StartsWith("Favourites") ?? false))
            {
                var favouriteResponse = await favouritesService.GetAllFavouritesPage(userName, request.PageSize, request.StartFile);

                foreach (var file in favouriteResponse.Files)
                {
                    PopulatePreview(file);
                }

                return ApplyExactFileFilter(favouriteResponse, request.ExactFileName);
            }

            var response = await MakeAuthenticatedRequest<FilesRequest, FilesResponse>(request, userName, "/b2api/v2/b2_list_file_names", cancellationToken);

            IEnumerable<string> favourites = Array.Empty<string>();

            if (favouritesService != null)
            {
                favourites = await favouritesService.GetAllFavourites(userName);
            }

            // populate thumbnails
            foreach (var file in response.Files)
            {
                PopulatePreview(file);

                file.IsFavoureite = favourites.Contains(file.FileName);
            }

            // Populate Tags
            var tags = await _tagService.GetTagsForFile(response.Files.Select(f => f.FileName));

            foreach(var file in response.Files)
            {
                if (tags.ContainsKey(file.FileName))
                {
                    file.Tags = tags[file.FileName];
                }
            }

            return ApplyExactFileFilter(response, request.ExactFileName);
        }

        public IResult Preview(string path)
        {
            if (!ThumbnailPathResolver.TryResolve(_options.THUMBNAIL_DIR, path, out var thumbnailPath))
            {
                return Results.NotFound();
            }

            var info = _thumbnails.Get(thumbnailPath);

            if (info is null)
            {
                return Results.NotFound();
            }

            // Serving the file from disk lets ASP.NET Core answer If-None-Match and
            // If-Modified-Since with a 304 instead of resending the bytes. The resolved
            // path is always rooted, so this produces a PhysicalFileHttpResult.
            return Results.File(
                thumbnailPath,
                contentType: "image/jpeg",
                lastModified: info.LastModified,
                entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue(
                    $"\"{info.LastModified.ToUnixTimeSeconds():x}-{info.Length:x}\""),
                enableRangeProcessing: true);
        }

        public async Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetReadCache(userName, cancellationToken);
            _httpClient.DefaultRequestHeaders.Clear();
            var stream = await _httpClient.GetSeekableStreamAsync(cacheModel.Token, $"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
            return Results.Stream(stream, contentType: stream.ContentType, enableRangeProcessing: true);
        }

        public async Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetReadCache(userName, cancellationToken);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            return await _httpClient.GetStreamAsync($"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
        }

        public async Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken)
        {
            var urlResponse = await GetUploadUri(cancellationToken);
            
            var streamContent = new StreamContent(content);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", urlResponse.Token);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-File-Name", HttpUtility.UrlEncode(fileName));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", "do_not_verify");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Info-Author", "Arkaine");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Server-Side-Encryption", "AES256");
            streamContent.Headers.ContentLength = length;
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var response = await _httpClient.PostAsync(urlResponse.UploadUrl, streamContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Upload API call responded with: {response.StatusCode}");
                throw new HttpRequestException($"Upload API call failed with status code {response.StatusCode}.");
            }

            await _hubContext.Clients.All.SendAsync("update", $"Upload single part file {fileName} succeeded", cancellationToken);
        }

        public async Task Delete(DeleteModel request, CancellationToken cancellationToken)
        {
            _ = await MakeAuthenticatedRequest<DeleteModel, DeleteModel>(
                request,
                WriteCacheKey,
                "/b2api/v2/b2_delete_file_version",
                cancellationToken,
                useWriteCredentials: true);

            await _hubContext.Clients.All.SendAsync("update", $"Delete file {request.FileName} succeeded", cancellationToken);
        }

        public async Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken)
        {
            ValidateMultipartUpload(content, chunkSize);

            // Check if this file is already partially uploaded.
            var unfinishedFilesResponse = await CheckForUnfinishedFile(fileName, cancellationToken);
            string fileId;

            if (unfinishedFilesResponse.Files.Count > 1)
            {
                throw new($"There are multiple files started named {fileName}");
            }
            else if (unfinishedFilesResponse.Files.Count == 1)
            {
                fileId = unfinishedFilesResponse.Files[0].Id;
            }
            else
            {
                // Start the upload
                var createFileResponse = await StartPartUpload(fileName, contentType, cancellationToken);
                fileId = createFileResponse.FileId;
            }

            // Get the upload URI
            var getUploadUriResponse = await GetPartUploadUri(fileId, cancellationToken);

            // Upload each chunk
            var partNumber = 1;
            var buffer = new byte[chunkSize];
            var shas = new List<string>();
            
            while (true)
            {
                int read = await content.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken);
                if (read < 1)
                {
                    _logger.LogInformation("No more bytes to read, finished upload");
                    break;
                }
                else
                {
                    _logger.LogInformation($"Bytes Read: {read}");
                }

                if (partNumber > B2MultipartLimits.MaximumPartCount)
                {
                    throw new InvalidOperationException(
                        $"Multipart uploads cannot exceed {B2MultipartLimits.MaximumPartCount} parts.");
                }

                await _hubContext.Clients.All.SendAsync("update", $"Download part {partNumber} succeeded", cancellationToken);
                var sha = await UploadPart(getUploadUriResponse.UploadUrl, getUploadUriResponse.AuthorizationToken, partNumber, buffer, read, cancellationToken);
                
                partNumber++;
                shas.Add(sha);
            }

            // Finish the upload
            await FinishUploadFile(fileId, shas, cancellationToken);
            await _hubContext.Clients.All.SendAsync("update", $"Upload multi part file {fileName} succeeded", cancellationToken);
        }

        private async Task<string> UploadPart(string url, string token, int partNumber, byte[] bytes, int count, CancellationToken cancellationToken)
        {
            // Compute the hash
            var hashBuilder = new StringBuilder();
            var sha = SHA1.HashData(bytes.AsSpan(0, count));
            foreach (byte b in sha)
            {
                hashBuilder.Append(b.ToString("x2"));
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Part-Number", partNumber.ToString());
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", hashBuilder.ToString());
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var content = new ByteArrayContent(bytes, 0, count);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Upload part resposne call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", "Upload part failed", cancellationToken);
                throw new HttpRequestException($"Upload part failed with status code {response.StatusCode}.");
            }
            
            await _hubContext.Clients.All.SendAsync("update", $"Upload part {partNumber} succeeded", cancellationToken);

            return hashBuilder.ToString();
        }

        private async Task<FinishUploadFileResponse> FinishUploadFile(string fileId, IEnumerable<string> hashes, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<FinishUploadFileRequest, FinishUploadFileResponse>(new FinishUploadFileRequest
            {
                FileId = fileId,
                Hashes = hashes,
            }, WriteCacheKey, "/b2api/v2/b2_finish_large_file", cancellationToken, useWriteCredentials: true);

            await _hubContext.Clients.All.SendAsync("update", $"Finish multi part file returned code: {response.Action}", cancellationToken);
            return response;
        }

        /// <summary>
        /// Resolves the thumbnail URL plus its intrinsic dimensions so the client can
        /// reserve exact space in the gallery before the image has loaded.
        /// </summary>
        private void PopulatePreview(B2File file)
        {
            ThumbnailPreview.Populate(file, _options.THUMBNAIL_DIR, _thumbnails);
        }

        private async Task<TResponse> MakeAuthenticatedRequest<TRequest, TResponse>(TRequest request, string userName, string url, CancellationToken cancellationToken, bool useWriteCredentials = false)
        {
            var cacheModel = useWriteCredentials
                ? await GetWriteCache(cancellationToken)
                : await GetReadCache(userName, cancellationToken);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = JsonSerializer.Serialize(request, options: new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var buffer = Encoding.UTF8.GetBytes(content);

            var byteContent = new ByteArrayContent(buffer);
            var response = await _httpClient.PostAsync(cacheModel.ApiUrl + url, byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"API call to {url} responded with: {response.StatusCode}");
                throw new HttpRequestException($"API call failed with status code {response.StatusCode}.");
            }

            var model = JsonSerializer.Deserialize<TResponse>(responseString) ?? throw new($"Response is not a valid format for {nameof(TResponse)}");
            _logger.LogInformation($"{url} call succeeded");

            return model;
        }

        private async Task<GetUploadPartsResponse> GetPartUploadUri(string fileId, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<GetUploadPartsRequest, GetUploadPartsResponse>(new GetUploadPartsRequest
            {
                FileId = fileId,
            }, WriteCacheKey, "/b2api/v2/b2_get_upload_part_url", cancellationToken, useWriteCredentials: true);
            await _hubContext.Clients.All.SendAsync("update", $"Get upload url for file succeeded", cancellationToken);
            return response;
        }

        private async Task<FilesResponse> CheckForUnfinishedFile(string fileName, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<CheckForUnfinishedFilesRequest, FilesResponse>(new CheckForUnfinishedFilesRequest
            {
                BucketId = _options.BUCKET_ID,
                NamePrefix = fileName
            }, WriteCacheKey, "/b2api/v2/b2_list_unfinished_large_files", cancellationToken, useWriteCredentials: true);

            response.Files = response.Files
                .Where(file => string.Equals(file.FileName, fileName, StringComparison.Ordinal))
                .ToList();
            await _hubContext.Clients.All.SendAsync("update", $"Check for unfinished files finished and found {response.Files.Count} files", cancellationToken);
            return response;
        }

        private static void ValidateMultipartUpload(Stream content, int chunkSize)
        {
            if (chunkSize < B2MultipartLimits.MinimumPartSizeBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    $"Multipart part size must be at least {B2MultipartLimits.MinimumPartSizeBytes} bytes.");
            }

            if (!content.CanSeek)
            {
                return;
            }

            var remainingLength = content.Length - content.Position;
            if (remainingLength > B2MultipartLimits.MaximumFileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Multipart upload size cannot exceed {B2MultipartLimits.MaximumFileSizeBytes} bytes.");
            }

            var requiredParts = (remainingLength + chunkSize - 1L) / chunkSize;
            if (requiredParts < 2)
            {
                throw new InvalidOperationException("Multipart uploads must contain at least two parts.");
            }

            if (requiredParts > B2MultipartLimits.MaximumPartCount)
            {
                throw new InvalidOperationException(
                    $"Multipart upload requires {requiredParts} parts; the maximum is {B2MultipartLimits.MaximumPartCount}.");
            }
        }

        private async Task<StartPartUploadResponse> StartPartUpload(string fileName, string contentType, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<StartPartUploadRequest, StartPartUploadResponse>(new StartPartUploadRequest
            {
                BucketId = _options.BUCKET_ID,
                ContentType = contentType,
                FileName = fileName
            }, WriteCacheKey, "/b2api/v2/b2_start_large_file", cancellationToken, useWriteCredentials: true);

            _logger.LogInformation("Start multi part file succeeded");
            await _hubContext.Clients.All.SendAsync("update", $"Start multi part file succeeded", cancellationToken);
            return response;
        }

        private async Task<UploadUrlResponse> GetUploadUri(CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<UploadUrlRequest, UploadUrlResponse>(new UploadUrlRequest
            {
                BucketId = _options.BUCKET_ID
            }, WriteCacheKey, "/b2api/v2/b2_get_upload_url", cancellationToken, useWriteCredentials: true);

            await _hubContext.Clients.All.SendAsync("update", $"Get upload url succeeded", cancellationToken);
            return response;
        }

        private static FilesResponse ApplyExactFileFilter(FilesResponse response, string? exactFileName)
        {
            if (string.IsNullOrWhiteSpace(exactFileName))
            {
                return response;
            }

            response.Files = response.Files
                .Where(file => string.Equals(file.FileName, exactFileName, StringComparison.Ordinal))
                .ToList();
            response.NextFileName = string.Empty;
            return response;
        }

        private Task<CacheModel> GetReadCache(string key, CancellationToken cancellationToken)
        {
            return GetCache(key, _options.B2_KEY_READ, cancellationToken);
        }

        private Task<CacheModel> GetWriteCache(CancellationToken cancellationToken)
        {
            return GetCache(WriteCacheKey, _options.B2_KEY_WRITE, cancellationToken);
        }

        private async Task<CacheModel> GetCache(string key, string credential, CancellationToken cancellationToken)
        {
            var cacheModel = _cache.Get(key) as CacheModel;

            if (cacheModel == null)
            {
                _logger.LogWarning($"Cache model not found for {key}");
                var response = await GetToken(credential, cancellationToken);
                cacheModel = new CacheModel(response.Token, response.DownloadBaseUrl, response.ApiBaseUrl, response.AccountId);
                _cache.Set(key, cacheModel, DateTime.UtcNow.AddHours(23));
            }

            return cacheModel;
        }
    }
}
