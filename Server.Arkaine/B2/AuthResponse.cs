namespace Server.Arkaine.B2
{
    public class AuthResponse
    {
        public int absoluteMinimumPartSize { get; set; }
        public string accountId { get; set; } = string.Empty;
        public Allowed allowed { get; set; } = new Allowed();
        public string apiUrl { get; set; } = string.Empty;
        public string authorizationToken { get; set; } = string.Empty;
        public string downloadUrl { get; set; } = string.Empty;
        public int recommendedPartSize { get; set; }
        public string s3ApiUrl { get; set; } = string.Empty;
    }

    public class Allowed
    {
        public string bucketId { get; set; } = string.Empty;
        public string bucketName { get; set; } = string.Empty;
        public List<string> capabilities { get; set; } = new List<string>();
        public string namePrefix { get; set; } = string.Empty;
    }
}
