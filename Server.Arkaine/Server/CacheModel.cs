namespace Server.Arkaine
{
    public class CacheModel
    {
        public CacheModel(string token, string downloadUrl, string apiUrl, string accountID)
        {
            Token = token;
            DownloadUrl = downloadUrl;
            ApiUrl = apiUrl;
            AccountId = accountID;
        }

        public string Token { get; }
        public string DownloadUrl { get; }
        public string ApiUrl { get; }
        public string AccountId { get; }
    }
}
