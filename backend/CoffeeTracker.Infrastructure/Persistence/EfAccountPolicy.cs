using CoffeeTracker.Application.Ports.Driven;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Reads and writes the instance's account policy from the singleton
/// <see cref="AppSettings"/> row.
/// </summary>
public sealed class EfAccountPolicy(AppDbContext db) : IAccountPolicy
{
    public async Task<AccountPolicy> GetAsync(CancellationToken ct = default)
    {
        var row = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == AppSettings.SingletonId, ct);

        return row is null
            ? await DefaultForUnseededInstanceAsync(ct)
            : new AccountPolicy(row.LocalLoginEnabled, row.LocalRegistrationEnabled, row.RegistrationOpenedForBootstrap);
    }

    public async Task SetAsync(AccountPolicy policy, CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Id == AppSettings.SingletonId, ct);
        if (row is null)
        {
            row = new AppSettings { Id = AppSettings.SingletonId };
            db.AppSettings.Add(row);
        }

        row.LocalLoginEnabled = policy.LocalLoginEnabled;
        row.LocalRegistrationEnabled = policy.LocalRegistrationEnabled;
        row.RegistrationOpenedForBootstrap = policy.RegistrationOpenedForBootstrap;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// What the policy means before the row is written. Startup seeding normally gets
    /// there first, so this is the answer for a context that was never seeded (tests,
    /// or a read racing the seeder on a fresh database): sign-in stays on, because no
    /// setting should ever be able to lock everyone out by being merely absent, and
    /// registration is open only while there is nobody to protect.
    /// </summary>
    private async Task<AccountPolicy> DefaultForUnseededInstanceAsync(CancellationToken ct) =>
        new(LocalLoginEnabled: true,
            LocalRegistrationEnabled: !await db.Users.AnyAsync(ct),
            RegistrationOpenedForBootstrap: !await db.Users.AnyAsync(ct));
}
