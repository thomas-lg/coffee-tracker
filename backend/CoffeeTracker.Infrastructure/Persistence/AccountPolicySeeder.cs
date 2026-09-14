using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Writes the singleton settings row the first time an instance runs with it. Kept
/// apart from the DI wiring because the rule it encodes, what an instance's account
/// policy should be before anyone has set one, is worth stating and testing on its
/// own.
/// </summary>
public static class AccountPolicySeeder
{
    /// <summary>
    /// Seeds the policy if it is absent, and does nothing at all once the row exists.
    ///
    /// Sign-in is always seeded on: no setting should be able to lock an instance out
    /// by being merely absent.
    ///
    /// Registration is seeded open only where there is nobody to protect, so an operator
    /// can create the first account with nothing configured, and that opening is marked
    /// as a bootstrap, so it shuts itself once the account exists. An instance that
    /// already has users has someone who can sign in and open registration deliberately,
    /// so it starts shut rather than standing open on the internet.
    ///
    /// Matches <see cref="EfAccountPolicy"/>'s answer for an instance whose row was never
    /// written; the two used to disagree, because this one also honoured a legacy
    /// environment variable.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
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
            // The two coincide here and only here: an administrator who later opens
            // registration sets the first without re-arming the second.
            LocalRegistrationEnabled = !hasUsers,
            RegistrationOpenedForBootstrap = !hasUsers,
        });
        await db.SaveChangesAsync(ct);
    }
}
