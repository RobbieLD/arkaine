using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public class UserService : IUserService
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public UserService(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public async Task<bool> LoginUserAsync(string username, string password, bool remember)
        {
            var result = await _signInManager.PasswordSignInAsync(username, password, remember, true);
            return result.RequiresTwoFactor;            
        }

        public async Task<IList<string>?> TwoFactorAuthenticateAsync(string code, string username, bool remember)
        {
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, remember, remember);
            
            if (!result.Succeeded)
            {
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            return await _userManager.GetRolesAsync(user);
        }
    }
}
