namespace Server.Arkaine.Ingest
{
    public interface IExtractor
    {
        Task<Stream> Extract(string url);
        string Bucket { get; }
    }
}
