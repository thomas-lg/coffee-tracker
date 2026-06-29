using CoffeeTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Seeds initial data into the database for development and testing.
/// </summary>
public static class DatabaseSeed
{
    /// <summary>
    /// Creates the test account if it doesn't exist.
    /// Used for e2e tests and local development.
    /// </summary>
    public static async Task SeedTestUserAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string testEmail = "test@example.com";
        const string testPassword = "password123";
        const string testDisplayName = "Test User";

        // Check if test user already exists
        var existingUser = await userManager.FindByEmailAsync(testEmail);
        if (existingUser != null)
        {
            logger.LogInformation("Test user already exists (email: {Email})", testEmail);
            return;
        }

        // Create test user
        var testUser = new AppUser
        {
            UserName = testEmail,
            Email = testEmail,
            DisplayName = testDisplayName,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(testUser, testPassword);
        if (result.Succeeded)
        {
            logger.LogInformation(
                "Created test user (email: {Email}, password: {Password}, displayName: {DisplayName})",
                testEmail,
                testPassword,
                testDisplayName);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create test user: {Errors}", errors);
        }
    }
}
