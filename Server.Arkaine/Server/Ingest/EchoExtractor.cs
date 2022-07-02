namespace Server.Arkaine.Ingest
{
    public class EchoExtractor : BaseExtractor, IExtractor
    {
        public EchoExtractor(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public string Bucket => "6e145527380c9fe78310051d";

        public async Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken)
        {
            return await OpenMediaStream(url, fileName, cancellationToken);
        }
    }
}
