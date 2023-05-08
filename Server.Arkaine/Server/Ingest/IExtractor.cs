namespace Server.Arkaine.Ingest
{
    public interface IExtractor
    {
        Task<ExtractorResponse> Extract(string url, string fileName, CancellationToken cancellationToken);
    }
}
