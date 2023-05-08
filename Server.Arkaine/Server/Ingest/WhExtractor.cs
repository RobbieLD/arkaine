using Newtonsoft.Json;

namespace Server.Arkaine.Ingest
{
    public class WhExtractor : BaseExtractor, IExtractor
    {
        public WhExtractor(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            var apiUrl = "https://api.whyp.it/api" + uri.PathAndQuery;
            var client = _httpClientFactory.CreateClient();
            string apiResponse = await client.GetStringAsync(apiUrl, cancellationToken);
            dynamic content = JsonConvert.DeserializeObject(apiResponse) ?? throw new("Api response was invalid");
            return await OpenMediaStream(content.track.audio_url, fileName, cancellationToken);
        }
    }
}
