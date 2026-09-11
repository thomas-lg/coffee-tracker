using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Writes the singleton settings row the first time an instance runs with it. Kept
/// apart from the DI wiring because the rule it encodes — what an instance's account
/// policy should be before anyone has set one — is worth stating and testing on its
/// own.
/// </summary>
public static class AccountPolicySeeder
{
    /// <summary>
    /// Seeds the policy if it is absent, and does nothing at all once the row exists.
    ///
    /// An instance that already has users is being upgraded: sign-in was always on, so
    /// it stays on, and registration takes <paramref name="legacyRegistrationEnabled"/>
    /// so the deployment's posture is preserved. That value comes from the legacy
    /// <c>REGISTRATION_ENABLED</c> variable, and this is the only place it is read.
    ///
    /// An instance with no users is fresh: both are opened so an operator can create
    /// the first account with nothing to configure, and the opening is marked as a
    /// bootstrap so it closes itself once that account exists.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, bool legacyRegistrationEnabled, CancellationToken ct = default)
    {
        if (await db.AppSettings.AnyAsync(s => s.Id == AppSettings.SingletonId, ct))
        {
            return;
        }

        var hasUsers = await db.Users.AnyAsync(ct);
        db.AppSettings.Add(new AppSettings
        {
            Id = AppSettings.SingletonId,
            LocalLoginEnabled = true,
            LocalRegistrationEnabled = hasUsers ? legacyRegistrationEnabled : true,
            RegistrationOpenedForBootstrap = !hasUsers,
        });
        await db.SaveChangesAsync(ct);
    }
}
