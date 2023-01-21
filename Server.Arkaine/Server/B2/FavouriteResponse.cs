using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class FavouriteResponse
    {
        [JsonPropertyName("action")]
        public string Result { get; set; } = string.Empty;
    }
}
