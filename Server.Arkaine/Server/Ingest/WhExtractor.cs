using Newtonsoft.Json;

namespace Server.Arkaine.Ingest
{
    public class WhExtractor : BaseExtractor, IExtractor
    {
        public WhExtractor(HttpClient httpClient) : base(httpClient)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            var apiUrl = "https://api.whyp.it/api" + uri.PathAndQuery;
            string apiResponse = await _httpClient.GetStringAsync(apiUrl, cancellationToken);
            dynamic content = JsonConvert.DeserializeObject(apiResponse) ?? throw new("Api response was invalid");
            return new ExtractorResponse(fileName, content.track.audio_url);
        }
    }
}
