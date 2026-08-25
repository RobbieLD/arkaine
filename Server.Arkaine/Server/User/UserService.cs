using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public class UserService : IUserService
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public UserService(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
            _signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }

        public async Task<SignInResult> LoginUserAsync(string username, string password, bool remember)
        {
            return await _signInManager.PasswordSignInAsync(username, password, remember, true);           
        }

        public async Task<IdentityUser?> TwoFactorAuthenticateAsync(string code, bool remember)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return null;
            }

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, remember, remember);
            
            if (!result.Succeeded)
            {
                return null;
            }

            return user;
        }
    }
}
