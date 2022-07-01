namespace Server.Arkaine.Ingest
{
    public class ExtractorFactory : IExtractorFactory
    {
        private readonly IServiceProvider _services;
        public ExtractorFactory(IServiceProvider services)
        {
            _services = services;
        }

        public IExtractor GetExtractor(string url)
        {
            if (url.Contains("soundgasm.net"))
            {
                return _services.GetRequiredService<SgExtractor>();
            }
            else
            {
                return _services.GetRequiredService<EchoExtractor>();
            }
        }
    }
}
