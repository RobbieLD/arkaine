using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class StartPartUploadRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;
    }
}
