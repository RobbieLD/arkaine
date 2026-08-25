using System.Text.Json;

namespace Server.Arkaine.User
{
    public sealed class PasskeyRequestOptionsRequest
    {
        public string? Username { get; set; }
    }

    public sealed class PasskeyLoginRequest
    {
        public JsonElement Credential { get; set; }
        public bool Remember { get; set; }
    }
}
