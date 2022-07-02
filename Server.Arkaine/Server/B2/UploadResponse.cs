using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class UploadResponse
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("contentLength")]
        [JsonConverter(typeof(ContentLengthCoverter))]
        public string Size { get; set; } = string.Empty;
    }
}
