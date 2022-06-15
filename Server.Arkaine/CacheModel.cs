namespace Server.Arkaine
{
    public class CacheModel
    {
        public CacheModel(string token, string downloadUrl, string apiUrl)
        {
            Token = token;
            DownloadUrl = downloadUrl;
            ApiUrl = apiUrl;
        }

        public string Token { get; }
        public string DownloadUrl { get; }
        public string ApiUrl { get; }
    }
}
