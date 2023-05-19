using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class GetUploadPartsRequest
    {
        [JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;
    }
}
