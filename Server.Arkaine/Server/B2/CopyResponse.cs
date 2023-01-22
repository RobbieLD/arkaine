using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class CopyResponse
    {
        [JsonPropertyName("action")]
        public string Result { get; set; } = string.Empty;
    }
}
