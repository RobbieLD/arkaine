using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class GetUploadPartsResponse
    {
        [JsonPropertyName("uploadUrl")]
        public string UploadUrl { get; set; } = string.Empty;
    }
}
