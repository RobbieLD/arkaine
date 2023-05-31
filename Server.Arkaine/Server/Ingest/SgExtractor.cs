using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public class SgExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = new(@"https:\/\/media.*..net\/sounds\/(\d|[a-z])*.m4a");
        public SgExtractor(HttpClient httpClient, ILogger<SgExtractor> logger) : base(httpClient, logger)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);
            var filePath = _exp.Match(response).Value;
            return await OpenMediaStream(filePath, fileName, cancellationToken);
        }
    }
}
