using Microsoft.Extensions.Options;
using System.Reflection;

namespace Server.Arkaine.Ingest
{
    public class ExtractorFactory : IExtractorFactory
    {
        private readonly IServiceProvider _services;
        private readonly ArkaineOptions _options;

        public ExtractorFactory(IServiceProvider services, IOptions<ArkaineOptions> config)
        {
            _services = services;
            _options = config.Value;
        }

        public IExtractor GetExtractor(string url)
        {
            var uri = new Uri(url);

            var keys = _options.SITE_KEYS.Split(",");
            var hostParts = uri.Host.Split('.');

            foreach (var key in keys)
            {
                var pair = key.Split(":");
                                
                if (hostParts[hostParts.Length - 2].EndsWith(pair[0]))
                {
                    var types = Assembly.GetExecutingAssembly().GetTypes();
                    var type = types.First(t => t.Name == pair[1]);
                    return (IExtractor)_services.GetRequiredService(type);
                }
            }
            
            // Default extractor which jsut gets the contents of the link as is
            return _services.GetRequiredService<EchoExtractor>();
        }
    }
}
