using FurnitureEcommerce.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Infrastructure.Identity;

/// <summary>Creates the first admin from settings. An existing account keeps its password.</summary>
public class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    AdminUserOptions options,
    ILogger<AdminSeeder> logger)
{
    public async Task SeedAsync()
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("'AdminUser' settings are not configured, so no admin account was created.");
            return;
        }

        var user = await userManager.FindByEmailAsync(options.Email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                FirstName = "Store",
                LastName = "Admin"
            };

            EnsureSucceeded(await userManager.CreateAsync(user, options.Password));
        }

        // Checked separately, so a run that stopped after creating the user is completed here.
        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.Admin));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"The admin account could not be created: {errors}");
        }
    }
}
