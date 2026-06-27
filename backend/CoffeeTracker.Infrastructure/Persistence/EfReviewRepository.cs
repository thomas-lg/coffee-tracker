using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>Driven adapter: EF Core implementation of the review repository port.</summary>
public class EfReviewRepository(AppDbContext db) : IReviewRepository
{
    // Newest-first: a user's ratings of a coffee form a timeline. Order by Id desc —
    // Id is monotonic with insertion, so it tracks creation order, and (unlike
    // DateTimeOffset, which SQLite stores as TEXT) it is orderable in SQL.
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

    public async Task<Review> AddAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review;
    }

    // `review` MUST be the tracked instance returned by GetByIdAsync (which eager-
    // loads Tags): SaveChanges alone then persists the mutated scalars and diffs the
    // re-built Tags join rows. Passing a detached entity would silently no-op, and
    // the re-added tags must also be tracked (see EfFlavorTagRepository.GetByIdsAsync)
    // or EF would try to INSERT duplicate FlavorTags.
    public async Task UpdateAsync(Review review, CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
    }
}
