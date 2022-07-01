namespace Server.Arkaine.Ingest
{
    public class EchoExtractor : IExtractor
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public EchoExtractor(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string Bucket => "rld-dev";

        public async Task<Stream> Extract(string url)
        {
            var client = _httpClientFactory.CreateClient();
            return await client.GetStreamAsync(url);
        }
    }
}
