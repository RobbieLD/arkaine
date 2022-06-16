using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Server.Arkaine.B2
{
    public class B2Service : IB2Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly ArkaineOptions _options;

        public B2Service(IHttpClientFactory httpClientFactory, IMemoryCache cache, IOptions<ArkaineOptions> config, ILogger<B2Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
            _options = config.Value;
        }

        public async Task<AuthResponse> GetToken(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.B2_KEY_ID + ":" + _options.B2_KEY));

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

        public async Task<AlbumsResponse> ListAlbums(AlbumsRequest request, string userName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var buffer = Encoding.UTF8.GetBytes("{\"accountId\":\"" + request.AccountId + "\"}");
            var byteContent = new ByteArrayContent(buffer);
            var response = await client.PostAsync(cacheModel.ApiUrl + "/b2api/v2/b2_list_buckets", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<AlbumsResponse>(responseString) ?? throw new("Bucket list response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"List buckets API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            _logger.LogInformation("List buckets succeeded");
            return responseModel;
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var buffer = Encoding.UTF8.GetBytes("{\"bucketId\":\"" + request.BucketId + "\"}");
            var byteContent = new ByteArrayContent(buffer);

            var response = await client.PostAsync(cacheModel.ApiUrl + "/b2api/v2/b2_list_file_names", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<FilesResponse>(responseString) ?? throw new("Files response is not a valid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"List files API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            _logger.LogInformation("List files succeeded");
            return responseModel;
        }

        public async Task<IResult> Stream(string bucketName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache("d", cancellationToken);
            var client = _httpClientFactory.CreateClient();
            var stream = await client.GetSeekableStreamAsync($"{cacheModel.DownloadUrl}/file/{bucketName}/{fileName}", cancellationToken);
            return Results.Stream(stream, contentType: stream.ContentType, enableRangeProcessing: true);
        }

        private async Task<CacheModel> GetCache(string key, CancellationToken cancellationToken)
        {
            var cacheModel = _cache.Get(key) as CacheModel;

            if (cacheModel == null)
            {
                _logger.LogWarning($"Cache model not found for {key}");
                var response = await GetToken(cancellationToken);
                cacheModel = new CacheModel(response.Token, response.DownloadBaseUrl, response.ApiBaseUrl);
                _cache.Set(key, cacheModel);
            }

            return cacheModel;
        }
    }
}
