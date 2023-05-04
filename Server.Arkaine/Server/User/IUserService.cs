namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<bool> LoginUserAsync(string username, string password, bool remember);
        Task<IList<string>?> TwoFactorAuthenticateAsync(string code, string username, bool remeber);
    }
}
