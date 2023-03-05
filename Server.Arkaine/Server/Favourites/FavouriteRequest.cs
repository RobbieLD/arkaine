using System.Text.Json.Serialization;

namespace Server.Arkaine.Favourites
{
    public class FavouriteRequest
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;
    }
}
