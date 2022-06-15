using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class AlbumsResponse
    {
        [JsonPropertyName("buckets")]
        public List<Bucket> Buckets { get; set; } = new List<Bucket>();
    }
}
