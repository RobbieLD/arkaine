using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FinishUploadFileResponse
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;
    }
}
