using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FinishUploadFileRequest
    {
        [JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("partSha1Array")]
        public IEnumerable<string>? Hashes { get; set; }
    }
}
