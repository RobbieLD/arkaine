namespace Server.Arkaine.Ingest
{
    public class ExtractorHandlerFactory
    {
        public static IExtractor GetExtractor(string url)
        {
            if (url.Contains("soundgasm.net"))
            {
                return new SgExtractor();
            }
            else
            {
                throw new NotSupportedException($"The following url isn't supported: {url}");
            }
        }
    }
}
