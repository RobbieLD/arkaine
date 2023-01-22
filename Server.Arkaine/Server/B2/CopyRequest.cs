using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class CopyRequest
    {
        [JsonPropertyName("sourceFileId")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }= string.Empty;
    }
}
