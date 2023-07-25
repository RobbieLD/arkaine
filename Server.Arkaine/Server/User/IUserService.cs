using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<SignInResult> LoginUserAsync(string username, string password, bool remember);
        Task<IList<string>?> TwoFactorAuthenticateAsync(string code, string username, bool remeber);
    }
}
