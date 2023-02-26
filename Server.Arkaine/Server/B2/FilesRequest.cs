using Newtonsoft.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FilesRequest
    {
        [JsonPropertyName("bucketId")]
        public string BucketId { get; set; } = string.Empty;

        [JsonPropertyName("prefix")]
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Prefix { get; set; }

        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("delimiter")]
        public string? Delimiter { get; set; }

        [JsonPropertyName("maxFileCount")]
        public int PageSize { get; set; }

        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("startFileName")]
        public string? StartFile { get; set; }

    }
}
