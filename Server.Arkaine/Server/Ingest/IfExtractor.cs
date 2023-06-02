using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public partial class IfExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = Regex();

        public IfExtractor(HttpClient httpClient) : base(httpClient)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);
            var chunks = _exp.Match(response).Value.Split("'");
            var filePath = "http://" + chunks[chunks.Length - 1][2..];
            return new ExtractorResponse(fileName, filePath);
        }

        [GeneratedRegex("src='\\/\\/.+\\/stream\\/[a-z0-9,-]+\\/[a-z0-9]+")]
        private static partial Regex Regex();
    }
}
