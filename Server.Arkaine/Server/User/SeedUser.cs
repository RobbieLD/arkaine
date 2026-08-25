using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Server.Arkaine.User
{
    public class SeedUser
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            foreach (var role in new[] { "User", "Admin" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    EnsureSucceeded(
                        await roleManager.CreateAsync(new IdentityRole(role)),
                        $"Creating role '{role}'");
                }
            }

            await EnsureUserAsync(
                userManager,
                configuration,
                "user",
                "user@localhost.com",
                "SEED_USER_PASSWORD",
                "SEED_USER_AUTHENTICATOR_KEY",
                "User");
            await EnsureUserAsync(
                userManager,
                configuration,
                "admin",
                "admin@localhost.com",
                "SEED_ADMIN_PASSWORD",
                "SEED_ADMIN_AUTHENTICATOR_KEY",
                "Admin");
        }

        private static async Task EnsureUserAsync(
            UserManager<IdentityUser> userManager,
            IConfiguration configuration,
            string userName,
            string email,
            string passwordSetting,
            string authenticatorKeySetting,
            string role)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user == null)
            {
                var password = configuration[passwordSetting];
                if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
                {
                    throw new InvalidOperationException(
                        $"{passwordSetting} must be set to a password of at least 16 characters.");
                }

                var authenticatorKey = configuration[authenticatorKeySetting];
                user = new IdentityUser
                {
                    Email = email,
                    UserName = userName,
                    EmailConfirmed = true,
                    TwoFactorEnabled = !string.IsNullOrWhiteSpace(authenticatorKey)
                };

                EnsureSucceeded(
                    await userManager.CreateAsync(user, password),
                    $"Creating user '{userName}'");

                if (!string.IsNullOrWhiteSpace(authenticatorKey))
                {
                    EnsureSucceeded(
                        await userManager.SetAuthenticationTokenAsync(
                            user,
                            TokenOptions.DefaultAuthenticatorProvider,
                            "AuthenticatorKey",
                            authenticatorKey),
                        $"Setting the authenticator key for '{userName}'");
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                EnsureSucceeded(
                    await userManager.AddToRoleAsync(user, role),
                    $"Adding '{userName}' to role '{role}'");
            }
        }

        private static void EnsureSucceeded(IdentityResult result, string operation)
        {
            if (!result.Succeeded)
            {
                var codes = string.Join(", ", result.Errors.Select(error => error.Code));
                throw new InvalidOperationException($"{operation} failed: {codes}");
            }
        }
    }
}
