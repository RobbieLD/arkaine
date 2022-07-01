using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public class SgExtractor : IExtractor
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Regex _exp = new(@"https:\/\/media.soundgasm.net\/sounds\/(\d|[a-z])*.m4a/g");
        public SgExtractor(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string Bucket => "rld-sg";

        public async Task<Stream> Extract(string url)
        {
            var client = _httpClientFactory.CreateClient();
            string response = await client.GetStringAsync(url);
            var filePath = _exp.Match(response).Value;
            return await client.GetStreamAsync(filePath);
        }
    }
}
