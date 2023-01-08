using Server.Arkaine.Meta;
using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class B2File
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [JsonPropertyName("contentLength")]
        [JsonConverter(typeof(ContentLengthCoverter))]
        public string Size { get; set; } = string.Empty;

        public Rating Rating { get; set; } = new Rating();
    }
}
