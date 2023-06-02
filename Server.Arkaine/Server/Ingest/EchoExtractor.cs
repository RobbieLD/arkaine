namespace Server.Arkaine.Ingest
{
    public class EchoExtractor : BaseExtractor, IExtractor
    {
        public EchoExtractor(HttpClient httpClient) : base(httpClient)
        {
        }

        public string Bucket => "6e145527380c9fe78310051d";

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            return await Task.FromResult(new ExtractorResponse(fileName, url));
        }
    }
}
