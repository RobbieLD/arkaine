using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Server.Arkaine.B2
{
    public class B2Service : IB2Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly ArkaineOptions _options;

        public B2Service(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IOptions<ArkaineOptions> config,
            ILogger<B2Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
            _options = config.Value;
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
            return responseModel;
        }

        public async Task AddToFavourites(FavouriteRequest request, string userName, CancellationToken cancellationToken)
        {
            var copyRequest = new CopyRequest
            {
                Id = request.Id,
                FileName = request.Destination
            };

            var model = await MakeAuthenticatedRequest<CopyRequest, CopyResponse>(copyRequest, userName, "/b2api/v2/b2_copy_file", cancellationToken);

            if (model.Result != "copy")
            {
                throw new($"File copy result was {model}");
            }

            // Now delete existing file 
            var deleteRequest = new DeleteModel
            {
                Id = request.Id,
                FileName = request.Source
            };

            await MakeAuthenticatedRequest<DeleteModel, DeleteModel>(deleteRequest, userName, "/b2api/v2/b2_delete_file_version", cancellationToken);
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken)
        {
            // Fill in config options in the request
            request.BucketId = _options.BUCKET_ID;
            request.PageSize = request.PageSize > 0 ? request.PageSize : int.Parse(_options.PAGE_SIZE);

            var response = await MakeAuthenticatedRequest<FilesRequest, FilesResponse>(request, userName, "/b2api/v2/b2_list_file_names", cancellationToken);

            // populate thumbnails
            foreach (var file in response.Files)
            {
                var thumb = Path.Combine(_options.THUMBNAIL_DIR, file.FileName);

                if (File.Exists(thumb))
                {
                    file.Thumbnail = thumb;
                }
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

        public async Task<UploadResponse> Upload(string fileName, string contentType, long length, StreamContent content, CancellationToken cancellationToken)
        {
            var urlResponse = await GetUploadUri(cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", urlResponse.Token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-File-Name", HttpUtility.UrlEncode(fileName));
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", "do_not_verify");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Info-Author", "Arkaine");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Server-Side-Encryption", "AES256");
            content.Headers.ContentLength = length;
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            
            var response = await client.PostAsync(urlResponse.UploadUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<UploadResponse>(responseString) ?? throw new("Upload response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Upload API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            return responseModel;
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

        private async Task<UploadUrlResponse> GetUploadUri(CancellationToken cancellationToken)
        {
            var auth = await GetToken(_options.B2_KEY_WRITE, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var req = new { bucketId = _options.BUCKET_ID };
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);
            
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_get_upload_url", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<UploadUrlResponse>(responseString) ?? throw new("Upload url response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Get Upload URL API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            return responseModel;
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

        public Task<UploadResponse> Upload(string bucketId, string fileName, string contentType, long length, StreamContent content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
