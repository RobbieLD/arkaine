using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class Bucket
    {
        [JsonPropertyName("bucketName")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;
    }
}
