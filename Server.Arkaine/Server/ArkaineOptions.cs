namespace Server.Arkaine
{
    public class ArkaineOptions
    {
        public string DB_CONNECTION_STRING { get; set; } = string.Empty;
        public string B2_KEY { get; set; } = string.Empty;
        public string B2_KEY_ID { get; set; } = string.Empty;
        public string B2AuthUrl { get; set; } = string.Empty;
        public string CORS_ORIGIN { get; set; } = string.Empty;
        public string ACCEPT_IP_RANGE { get; set; } = string.Empty;
    }
}
