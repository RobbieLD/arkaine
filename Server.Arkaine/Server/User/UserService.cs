using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public class UserService : IUserService
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<UserService> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public UserService(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ILogger<UserService> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<bool> LoginUserAsync(string username, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(username, password, false, true);
            return result.RequiresTwoFactor;            
        }

        public async Task<IList<string>?> TwoFactorAuthenticateAsync(string code, string username)
        {
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, false, false);
            
            if (!result.Succeeded)
            {
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            return await _userManager.GetRolesAsync(user);
        }
    }
}
