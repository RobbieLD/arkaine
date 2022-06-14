using System.Net.Http.Headers;
using System.Text;

namespace Server.Arkaine.B2
{
    public class B2Service : IB2Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        public B2Service(IHttpClientFactory httpClientFactory, ILogger<B2Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> GetToken(string db2Key, string db2KeyId, string url)
        {
            var client = _httpClientFactory.CreateClient();
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(db2KeyId + ":" + db2Key));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Auth API call responded with: {response.StatusCode}");
                throw new (responseString);
            }

            _logger.LogInformation("Get token succeeded");
            return responseString;
        }

        public async Task<string> ListAlbums(AlbumsRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", request.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var buffer = Encoding.UTF8.GetBytes("{\"accountId\":\"" + request.AccountId + "\"}");
            var byteContent = new ByteArrayContent(buffer);

            var response = await client.PostAsync(request.Url + "/b2api/v2/b2_list_buckets", byteContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"List buckets API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            _logger.LogInformation("List buckets succeeded");
            return responseString;
        }

        public async Task<string> ListFiles(FilesRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", request.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var buffer = Encoding.UTF8.GetBytes("{\"bucketId\":\"" + request.BucketId + "\"}");
            var byteContent = new ByteArrayContent(buffer);

            var response = await client.PostAsync(request.Url + "/b2api/v2/b2_list_file_names", byteContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"List files API call responded with: {response.StatusCode}");
                throw new(responseString);
            }

            _logger.LogInformation("List files succeeded");
            return responseString;
        }
    }
}
