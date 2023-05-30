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
using System;

namespace Server.Arkaine.B2
{
    public class B2Service : IB2Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IHubContext<IngestHub> _hubContext;
        private readonly ArkaineOptions _options;

        public B2Service(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IOptions<ArkaineOptions> config,
            IHubContext<IngestHub> hubContext,
            ILogger<B2Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
            _options = config.Value;
            _hubContext = hubContext;
        }

        public async Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(_options.B2AuthUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<AuthResponse>(responseString) ?? throw new("Response not in the correct form");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Auth API call responded with: {response.StatusCode}");
                throw new (responseString);
            }

            _logger.LogInformation("Get token succeeded");

            await _hubContext.Clients.All.SendAsync("update", "Get token succeeded", cancellationToken);
            
            return responseModel;
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken)
        {
            // Fill in config options in the request
            request.BucketId = _options.BUCKET_ID;
            request.PageSize = request.PageSize > 0 ? request.PageSize : int.Parse(_options.PAGE_SIZE);

            // This is a special case of a pseudo collection
            if (favouritesService != null && (request.Prefix?.StartsWith("Favourites") ?? false))
            {
                var favouriteResponse = await favouritesService.GetAllFavouritesPage(userName, request.PageSize, request.StartFile);

                foreach (var file in favouriteResponse.Files)
                {
                    file.Thumbnail = GetPreviewUrl(file.FileName, file.Type);
                }

                return favouriteResponse;
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
                file.Thumbnail = GetPreviewUrl(file.FileName, file.Type);

                file.IsFavoureite = favourites.Contains(file.FileName);
            }

            return response;
        }

        public IResult Preview(string path)
        {
            return Results.Stream(File.OpenRead(path));
        }

        public async Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            var stream = await client.GetSeekableStreamAsync(cacheModel.Token, $"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
            return Results.Stream(stream, contentType: stream.ContentType, enableRangeProcessing: true);
        }

        public async Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            return await client.GetStreamAsync($"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
        }

        public async Task Upload(string fileName, string contentType, Stream content, CancellationToken cancellationToken)
        {
            // Check if this file is already partially uploaded.
            var unfinishedFilesResponse = await CheckForUnfinishedFile(fileName, cancellationToken);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
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
            var buffer = new byte[_options.UPLOAD_CHUNK_SIZE];
            var shas = new List<string>();
            
            while (true)
            {
                int read = await content.ReadAsync(buffer, cancellationToken);
                _logger.LogInformation($"Stream Read: {read}");

                if (read < 1) break;

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

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Part-Number", partNumber.ToString());
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", hashBuilder.ToString());
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var content = new ByteArrayContent(bytes, 0, count);

            var response = await client.PostAsync(url, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Upload part resposne call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Upload part failed with {responseString}", cancellationToken);
                throw new(responseString);
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
            }, "api", "/b2api/v2/b2_finish_large_file", cancellationToken);

            await _hubContext.Clients.All.SendAsync("update", $"Finish multi part file returned code: {response.Action}", cancellationToken);
            return response;
        }

        private string GetPreviewUrl(string fileName, string type)
        {
            var thumb = Path.Combine(_options.THUMBNAIL_DIR, fileName);

            if (type == "folder")
            {
                thumb = Path.Combine(thumb, "thumb.jpg");
            }

            if (File.Exists(thumb))
            {
                return thumb;
            }

            return string.Empty;
        }

        private async Task<TResponse> MakeAuthenticatedRequest<TRequest, TResponse>(TRequest request, string userName, string url, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = JsonSerializer.Serialize(request, options: new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var buffer = Encoding.UTF8.GetBytes(content);

            var byteContent = new ByteArrayContent(buffer);
            var response = await client.PostAsync(cacheModel.ApiUrl + url, byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"API call to {url} responded with: {response.StatusCode}");
                throw new(responseString);
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
            }, "api", "/b2api/v2/b2_get_upload_part_url", cancellationToken);
            await _hubContext.Clients.All.SendAsync("update", $"Get upload url for file succeeded", cancellationToken);
            return response;
        }

        private async Task<FilesResponse> CheckForUnfinishedFile(string fileName, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<CheckForUnfinishedFilesRequest, FilesResponse>(new CheckForUnfinishedFilesRequest
            {
                BucketId = _options.BUCKET_ID,
                NamePrefix = fileName
            }, "api", "/b2api/v2/b2_list_unfinished_large_files", cancellationToken);

            await _hubContext.Clients.All.SendAsync("update", $"Check for unfinished files finished and found {response.Files.Count} files", cancellationToken);
            return response;
        }

        private async Task<StartPartUploadResponse> StartPartUpload(string fileName, string contentType, CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<StartPartUploadRequest, StartPartUploadResponse>(new StartPartUploadRequest
            {
                BucketId = _options.BUCKET_ID,
                ContentType = contentType,
                FileName = fileName
            }, "api", "/b2api/v2/b2_start_large_file", cancellationToken);

            _logger.LogInformation("Start multi part file succeeded");
            await _hubContext.Clients.All.SendAsync("update", $"Start multi part file succeeded", cancellationToken);
            return response;
        }

        private async Task<UploadUrlResponse> GetUploadUri(CancellationToken cancellationToken)
        {
            var response = await MakeAuthenticatedRequest<UploadUrlRequest, UploadUrlResponse>(new UploadUrlRequest
            {
                BucketId = _options.BUCKET_ID
            }, "api", "/b2api/v2/b2_get_upload_url", cancellationToken);

            await _hubContext.Clients.All.SendAsync("update", $"Get upload url succeeded", cancellationToken);
            return response;
        }

        private async Task<CacheModel> GetCache(string key, CancellationToken cancellationToken)
        {
            var cacheModel = _cache.Get(key) as CacheModel;

            if (cacheModel == null)
            {
                _logger.LogWarning($"Cache model not found for {key}");
                var response = await GetToken(_options.B2_KEY_READ, cancellationToken);
                cacheModel = new CacheModel(response.Token, response.DownloadBaseUrl, response.ApiBaseUrl, response.AccountId);
                _cache.Set(key, cacheModel);
            }

            return cacheModel;
        }
    }
}
