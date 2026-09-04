using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public sealed class DownloadAuthorizationRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;

        [JsonPropertyName("fileNamePrefix")]
        public string FileNamePrefix { get; set; } = string.Empty;

        [JsonPropertyName("validDurationInSeconds")]
        public int ValidDurationInSeconds { get; set; }
    }

    public sealed class DownloadAuthorizationResponse
    {
        [JsonPropertyName("authorizationToken")]
        public string AuthorizationToken { get; set; } = string.Empty;
    }
}
