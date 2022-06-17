using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FilesResponse
    {
        [JsonPropertyName("files")]
        public List<B2File> Files { get; set; } = new List<B2File>();
    }
}
