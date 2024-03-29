﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Server.Arkaine.User
{
    public class SeedUser
    {
        public async static Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetService<ArkaineDbContext>() ?? throw new("Db Context Not Created");
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            string[] roles = new string[] { "User", "Admin" };

            foreach (string role in roles)
            {
                var roleStore = new RoleStore<IdentityRole>(context);

                if (!context!.Roles.Any(r => r.Name == role))
                {
                    await roleStore.CreateAsync(new IdentityRole
                    {
                        Name = role,
                        NormalizedName = role.ToUpper(),
                    });
                }
            }

            var user = new IdentityUser
            {
                Email = "user@localhost.com",
                NormalizedEmail = "USER@LOCALHOST.COM",
                UserName = "user",
                NormalizedUserName = "USER",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("D"),
                TwoFactorEnabled = true,
            };

            var admin = new IdentityUser
            {
                Email = "admin@localhost.com",
                NormalizedEmail = "ADMIN@LOCALHOST.COM",
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("D"),
                TwoFactorEnabled = true,
            };

            if (!context!.Users.Any(u => u.UserName == user.UserName))
            {
                await userManager.CreateAsync(user, ".Password1");
                var dbUser = await userManager.FindByNameAsync("user") ?? throw new("User not found");
                await userManager.AddToRolesAsync(dbUser, new[] { "User" });
            }


            if (!context!.Users.Any(u => u.UserName == admin.UserName))
            {
                await userManager.CreateAsync(admin, ".Password1");
                var dbAdmin = await userManager.FindByNameAsync("admin") ?? throw new("User not found");
                await userManager.AddToRolesAsync(dbAdmin, new[] { "Admin" });
            }

            await context.SaveChangesAsync();

            await userManager.ResetAuthenticatorKeyAsync(user);
            Console.WriteLine("Authenticator Key: " + user.UserName + ": " + await userManager.GetAuthenticatorKeyAsync(user));

            await userManager.ResetAuthenticatorKeyAsync(admin);
            Console.WriteLine("Authenticator Key: " + admin.UserName + ": " + await userManager.GetAuthenticatorKeyAsync(admin));
        }

    }
}
