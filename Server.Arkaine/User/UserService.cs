using Microsoft.AspNetCore.Identity;

namespace Server.Arkaine.User
{
    public class UserService : IUserService
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<UserService> _logger;
        public UserService(SignInManager<IdentityUser> signInManager, ILogger<UserService> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public Task<string> GetB2Token(string bucket)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> LoginUserAsync(string username, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(username, password, false, true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User sign in succeeded");
            }
            else
            {
                _logger.LogInformation("User signin failed");
            }

            return result.Succeeded;
        }
    }
}
