using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>Driven adapter: EF Core implementation of the review repository port.</summary>
public class EfReviewRepository(AppDbContext db) : IReviewRepository
{
    public async Task<IReadOnlyList<Review>> GetByCoffeeAsync(int coffeeId, CancellationToken ct = default) =>
        await db.Reviews
            .AsNoTracking()
            .Include(r => r.Tags)
            .Where(r => r.CoffeeId == coffeeId)
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);

    // Tracked + tags loaded: the update/delete paths mutate this instance (and its
    // Tags collection), so it must be attached to the change tracker.
    public async Task<Review?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Reviews
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<bool> ExistsForUserAsync(int coffeeId, string userId, CancellationToken ct = default) =>
        await db.Reviews.AnyAsync(r => r.CoffeeId == coffeeId && r.UserId == userId, ct);

    public async Task<Review> AddAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The (CoffeeId, UserId) unique index is the race backstop: two
            // concurrent creates can both pass the service's pre-check. Translate
            // the constraint violation into a typed result so the API returns 409
            // rather than leaking a 500.
            throw new DuplicateReviewException(ex);
        }

        return review;
    }

    // `review` MUST be the tracked instance returned by GetByIdAsync (which eager-
    // loads Tags): SaveChanges alone then persists the mutated scalars and diffs the
    // re-built Tags join rows. Passing a detached entity would silently no-op, and
    // the re-added tags must also be tracked (see EfFlavorTagRepository.GetByIdsAsync)
    // or EF would try to INSERT duplicate FlavorTags.
    public async Task UpdateAsync(Review review, CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);

    // A UNIQUE constraint failure is SQLITE_CONSTRAINT (19) whose message names the
    // failure as UNIQUE — distinguishing it from other code-19 violations (FK, NOT
    // NULL, CHECK) so those still surface normally instead of as "already reviewed".
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && sqlite.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);

    public async Task DeleteAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
    }
}
