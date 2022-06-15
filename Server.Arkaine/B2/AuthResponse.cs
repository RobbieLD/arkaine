using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class AuthResponse
    {
        [JsonPropertyName("authorizationToken")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadBaseUrl { get; set; } = string.Empty;

        [JsonPropertyName("apiUrl")]
        public string ApiBaseUrl { get; set; } = string.Empty;
    }
}
