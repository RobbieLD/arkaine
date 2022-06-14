using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

            var resposne = await client.GetAsync(url);

            if (!resposne.IsSuccessStatusCode)
            {
                throw new ($"Auth API call responded with: {resposne.StatusCode}");
            }

            return await resposne.Content.ReadAsStringAsync();
        }
    }
}
