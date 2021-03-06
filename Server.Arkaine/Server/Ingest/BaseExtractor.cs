namespace Server.Arkaine.Ingest
{
    public abstract class BaseExtractor
    {
        protected readonly IHttpClientFactory _httpClientFactory;

        public BaseExtractor(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        protected async Task<ExtractorResponse> OpenMediaStream(string url, string fileName, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var ext = Path.GetExtension(url);
            var contentResponse = await client.GetAsync(url, cancellationToken);

            if (!contentResponse.IsSuccessStatusCode)
            {
                throw new($"Extract failed with status code: {contentResponse.StatusCode}");
            }

            var content = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);

            // TODO: handle seituations where content length/media type is not returned
            return new ExtractorResponse(content, fileName + ext, contentResponse.Content.Headers.ContentType?.MediaType ?? string.Empty, contentResponse.Content.Headers.ContentLength ?? 0);
        }
    }
}
