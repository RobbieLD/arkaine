using Newtonsoft.Json;

namespace Server.Arkaine.Ingest
{
    public class WhExtractor : BaseExtractor, IExtractor
    {
        public string Bucket => "be943557a8ccaf478310051d";

        public WhExtractor(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            var apiUrl = "https://api.whyp.it/api" + uri.PathAndQuery;
            var client = _httpClientFactory.CreateClient();
            string apiResponse = await client.GetStringAsync(url, cancellationToken);
            dynamic content = JsonConvert.DeserializeObject(apiResponse);
            return await OpenMediaStream(content.track.audio_url, fileName, cancellationToken);
        }
    }
}
