using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class UploadUrlRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;
    }
}
