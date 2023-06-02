namespace Server.Arkaine.Ingest
{
    public class ExtractorResponse
    {
        public ExtractorResponse(string fileName, string url)
        {
            FileName = fileName;
            Url = url;
        }

        public string Url { get; }

        public string FileName { get; }

    }
}
