namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<bool> LoginUserAsync(string username, string password);
        Task<string> GetB2Token(string bucket);
    }
}
