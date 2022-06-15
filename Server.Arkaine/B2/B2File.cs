using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class B2File
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;
    }
}
