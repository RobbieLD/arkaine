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

        public async Task<SignInResult> PasskeyLoginAsync(string credentialJson, bool remember)
        {
            var result = await _signInManager.PasskeySignInAsync(credentialJson);
            if (!result.Succeeded || !remember)
            {
                return result;
            }

            var user = await _signInManager.UserManager.GetUserAsync(_signInManager.Context.User)
                ?? throw new InvalidOperationException("Passkey sign-in did not produce an authenticated user.");

            await _signInManager.SignInAsync(user, isPersistent: true, authenticationMethod: "passkey");
            return result;
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
