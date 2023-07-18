using Microsoft.Extensions.Options;
using Server.Arkaine.B2;

namespace Server.Arkaine.Ingest
{
    public abstract class BaseExtractor
    {
        protected readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly ArkaineOptions _options;

        public BaseExtractor(HttpClient httpClient, ILogger<IExtractor> logger, IOptions<ArkaineOptions> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = config.Value;
        }

        protected async Task<ExtractorResponse> OpenMediaStream(string url, string fileName, CancellationToken cancellationToken)
        {
            var ext = Path.GetExtension(url);
            var contentResponse = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!contentResponse.IsSuccessStatusCode)
            {
                throw new($"Extract failed with status code: {contentResponse.StatusCode}");
            }

            if (string.IsNullOrEmpty(ext) && !string.IsNullOrEmpty(contentResponse.Content.Headers.ContentType?.MediaType))
            {
                ext = "." + contentResponse.Content.Headers.ContentType?.MediaType?.Split("/")[1];
            }

            _logger.LogInformation($"Media stream content length: {contentResponse.Content.Headers.ContentLength}");
            _logger.LogInformation($"Media stream headers {contentResponse.Content.Headers}");

            var content = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);

            _logger.LogInformation($"Stream is: {content.GetType().Name}");

            // Note: The int to long cast below is safe because the length has already been checked and is known to be below the max int value
            // TODO: handle situations where content length/media type is not returned
            return new ExtractorResponse(
                content,
                fileName + ext,
                contentResponse.Content.Headers.ContentType?.MediaType ?? string.Empty,
                contentResponse.Content.Headers.ContentLength ?? 0,
                _options.UPLOAD_CHUNK_SIZE);
        }
    }
}
