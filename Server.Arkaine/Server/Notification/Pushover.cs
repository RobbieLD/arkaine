using Microsoft.Extensions.Options;

namespace Server.Arkaine.Notification
{
    public class Pushover : INotifier
    {
        private readonly ILogger<Pushover> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ArkaineOptions _options;

        public Pushover(IHttpClientFactory httpClientFactory, IOptions<ArkaineOptions> config, ILogger<Pushover> logger)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = config.Value;
        }

        public async Task Send(string message)
        {
#if DEBUG
            return;
#endif
            var client = _httpClientFactory.CreateClient();
            var request = new FormUrlEncodedContent(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("token", _options.PUSHOVER_TOKEN),
                new KeyValuePair<string, string>("user", _options.PUSHOVER_USER),
                new KeyValuePair<string, string>("message", message),
            });

            await client.PostAsync(_options.PushoverUrl, request);
            _logger.LogInformation($"Pushover message sent");
        }
    }
}
