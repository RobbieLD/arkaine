using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FilesRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;

        [JsonPropertyName("prefix")]
        public string Prefix { get; set; } = string.Empty;

        [JsonPropertyName("delimiter")]
        public string Delimiter { get; set; } = string.Empty;

        [JsonPropertyName("maxFileCount")]
        public int PageSize { get; set; }

        [JsonPropertyName("startFileName")]
        public string StartFile { get; set; } = string.Empty;

    }
}
