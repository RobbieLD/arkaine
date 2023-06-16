using Server.Arkaine.Tags;
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

        [JsonPropertyName("action")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("fileId")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("preview")]
        public string Thumbnail { get; set; } = string.Empty;

        [JsonPropertyName("favourite")]
        public bool IsFavoureite { get; set; }

        [JsonPropertyName("tags")] 
        public IEnumerable<Tag> Tags { get; set; } = Array.Empty<Tag>();
    }
}
