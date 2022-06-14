namespace Server.Arkaine.B2
{
    public abstract class BaseRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
