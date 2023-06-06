using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public partial class IfExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = Regex();

        public IfExtractor(HttpClient httpClient, ILogger<IExtractor> logger, IOptions<ArkaineOptions> config) : base(httpClient, logger, config)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);
            var chunks = _exp.Match(response).Value.Split("'");
            var filePath = "http://" + chunks[chunks.Length - 1].Substring(2);
            return await OpenMediaStream(filePath, fileName, cancellationToken);
        }

        [GeneratedRegex("src='\\/\\/.+\\/stream\\/[a-z0-9,-]+\\/[a-z0-9]+")]
        private static partial Regex Regex();
    }
}
