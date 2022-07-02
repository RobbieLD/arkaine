namespace Server.Arkaine.Ingest
{
    public class ExtractorResponse
    {
        public ExtractorResponse(Stream content, string fileName, string mimeType, long length)
        {
            Content = content;
            FileName = fileName;
            MimeType = mimeType;
            Length = length;
        }

        public Stream Content { get; }
        public string FileName { get; }
        public string MimeType { get; }
        public long Length { get; }

    }
}
