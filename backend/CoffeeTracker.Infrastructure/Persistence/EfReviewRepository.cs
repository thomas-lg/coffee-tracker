using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
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
        await db.SaveChangesAsync(ct);
        return review;
    }

    // `review` is the tracked instance from GetByIdAsync, so SaveChanges alone
    // persists the mutated scalars and the re-built Tags join rows.
    public async Task UpdateAsync(Review review, CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
    }
}
