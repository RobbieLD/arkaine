using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class StartPartUploadResponse
    {
        [JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;
    }
}
