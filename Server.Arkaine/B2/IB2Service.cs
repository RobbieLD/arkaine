namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<string> GetToken(string db2Key, string db2KeyId, string url);
    }
}
