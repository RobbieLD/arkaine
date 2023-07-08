using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Server.Arkaine.Ingest
{
    public partial class LrExtractor : BaseExtractor, IExtractor
    {
        private readonly Regex _exp = Regex();

        public LrExtractor(HttpClient httpClient, ILogger<LrExtractor> logger, IOptions<ArkaineOptions> config) : base(httpClient, logger, config)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);

            var filePath = _exp.Match(response).Value;
            return await OpenMediaStream(filePath, fileName, cancellationToken);
        }

        [GeneratedRegex("https:\\/\\/uploads\\.[a-z]+\\.com\\/audio\\/(\\d|[-a-z])*.m4a")]
        private static partial Regex Regex();
    }
}
