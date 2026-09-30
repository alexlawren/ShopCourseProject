using Microsoft.AspNetCore.Identity;
using Shop.IdentityService.Domain.Constants;
using Shop.IdentityService.Domain.Entities;

namespace Shop.IdentityService.Infrastructure.Identity;

/// <summary>
/// Seeds required roles and optionally bootstraps an Admin user.
/// Idempotent — safe to run on every startup.
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RoleManager<IdentityRole<Guid>>>>();

        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (result.Succeeded)
                    logger.LogInformation("Created role '{Role}'.", roleName);
                else
                    logger.LogWarning("Failed to create role '{Role}': {Errors}.",
                        roleName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        await SeedBootstrapAdminAsync(scope.ServiceProvider, logger);
    }

    private static async Task SeedBootstrapAdminAsync(IServiceProvider sp, ILogger logger)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["BootstrapAdmin:Email"];
        var password = config["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return; // Neither configured — skip silently.

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "BootstrapAdmin is partially configured (Email or Password is missing). " +
                "Skipping admin user creation.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            // Ensure the role is assigned even if the user already exists.
            if (!await userManager.IsInRoleAsync(existing, AppRoles.Admin))
                await userManager.AddToRoleAsync(existing, AppRoles.Admin);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(admin, password);
        if (!createResult.Succeeded)
        {
            logger.LogWarning("Failed to create bootstrap admin: {Errors}.",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Bootstrap admin user created (email not logged for security).");
    }
}
