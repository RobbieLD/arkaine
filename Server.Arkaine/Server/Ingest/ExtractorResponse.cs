namespace Server.Arkaine.Ingest
{
    public class ExtractorResponse
    {
        public ExtractorResponse(Stream content, string fileName, string mimeType, long length, int chunkSize)
        {
            Content = content;
            FileName = fileName;
            MimeType = mimeType;
            Length = length;
            ChunkSize = chunkSize;
        }

        public Stream Content { get; }
        public string FileName { get; }
        public string MimeType { get; }
        public long Length { get; }
        public int ChunkSize { get; }

    }
}
