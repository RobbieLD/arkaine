using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public class IfExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = new(@"src='\/\/.+\/stream\/[a-z0-9,-]+\/[a-z0-9]+");

        public IfExtractor(HttpClient httpClient, ILogger<IExtractor> logger) : base(httpClient, logger)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);
            var chunks = (_exp.Match(response).Value).Split("'");
            var filePath = "http://" + chunks[chunks.Length - 1].Substring(2);
            return await OpenMediaStream(filePath, fileName, cancellationToken);
        }
    }
}
