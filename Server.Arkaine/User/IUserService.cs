namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<User> LoginUserAsync(string username, string password);
        Task<string> GetB2Token(string bucket);
    }
}
