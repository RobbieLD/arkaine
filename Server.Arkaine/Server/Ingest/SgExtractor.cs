using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public class SgExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = new(@"https:\/\/media.*..net\/sounds\/(\d|[a-z])*.m4a");
        public SgExtractor(IHttpClientFactory httpClientFactory, ILogger<SgExtractor> logger) : base(httpClientFactory, logger)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            string response = await client.GetStringAsync(url, cancellationToken);
            var filePath = _exp.Match(response).Value;
            return await OpenMediaStream(filePath, fileName, cancellationToken);
        }
    }
}
