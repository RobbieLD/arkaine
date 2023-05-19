using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class CheckForUnfinishedFilesRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;

        [JsonPropertyName("namePrefix")]
        public string NamePrefix { get; set; } = string.Empty;
    }
}
