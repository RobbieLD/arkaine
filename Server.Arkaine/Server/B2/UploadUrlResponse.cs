using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class UploadUrlResponse
    {
        [JsonPropertyName("uploadUrl")]
        public string UploadUrl { get; set; } = string.Empty;

        [JsonPropertyName("authorizationToken")]
        public string Token { get; set; } = string.Empty;
    }
}
