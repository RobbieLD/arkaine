namespace Server.Arkaine.Ingest
{
    public interface IExtractorFactory
    {
        IExtractor GetExtractor(string url);
    }
}
