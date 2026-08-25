using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<SignInResult> LoginUserAsync(string username, string password, bool remember);
        Task<IdentityUser?> TwoFactorAuthenticateAsync(string code, bool remember);
    }
}
