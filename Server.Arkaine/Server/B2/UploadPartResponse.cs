using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class UploadPartResponse
    {
        [JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("partNumber")]
        public string PartNumber { get; set; } = string.Empty;

        [JsonPropertyName("contentLength")]
        public long ContentLength { get; set; }

        [JsonPropertyName("ContentSha1")]
        public string ContentSha1 { get; set; } = string.Empty;
    }
}
