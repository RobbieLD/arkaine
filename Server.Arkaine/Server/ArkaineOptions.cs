namespace Server.Arkaine
{
    public class ArkaineOptions
    {
        public string DB_CONNECTION_STRING { get; set; } = string.Empty;
        public string B2_KEY_READ { get; set; } = string.Empty;
        public string B2_KEY_WRITE { get; set; } = string.Empty;
        public string B2AuthUrl { get; set; } = string.Empty;
        public string CORS_ORIGIN { get; set; } = string.Empty;
        public string ACCEPT_IP_RANGE { get; set; } = string.Empty;
        public string MAX_COOKIE_LIFETIME { get; set; } = string.Empty;
        public string BUCKETS { get; set; } = string.Empty;
        public string API_KEY { get; set; } = string.Empty;
        public string SITE_KEYS { get; set; } = string.Empty;
    }
}
