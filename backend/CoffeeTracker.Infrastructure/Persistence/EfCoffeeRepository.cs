using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Driven adapter: EF Core implementation of the coffee repository port.
/// </summary>
public class EfCoffeeRepository(AppDbContext db) : ICoffeeRepository
{
    private sealed record StatRow(Coffee Coffee, double? AverageRating, int ReviewCount);

    // Projects a coffee query to its scalar review aggregates. The average/count are
    // correlated subqueries — ONE SQL query, no N+1, and AVG over no rows yields null
    // via the nullable cast. Flavour tags are loaded separately (see AttachTagsAsync):
    // SQLite can't translate a collection subquery nested alongside these scalars.
    private IQueryable<StatRow> WithScalarStats(IQueryable<Coffee> source) =>
        source.Select(c => new StatRow(
            c,
            db.Reviews.Where(r => r.CoffeeId == c.Id).Average(r => (double?)r.Rating),
            db.Reviews.Count(r => r.CoffeeId == c.Id)));

    // Distinct flavour tags per coffee, fetched in a single flat join query and grouped
    // in memory, then stitched onto the scalar rows (preserving their order).
    private async Task<IReadOnlyList<CoffeeWithStats>> AttachTagsAsync(
        IReadOnlyList<StatRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Coffee.Id).ToList();
        // Per-review collection projection (a LEFT JOIN, not a correlated SelectMany) so
        // SQLite never needs APPLY; flatten + dedupe per coffee in memory below.
        var perReview = await db.Reviews
            .Where(r => ids.Contains(r.CoffeeId))
            .Select(r => new { r.CoffeeId, Names = r.Tags.Select(t => t.Name).ToList() })
            .ToListAsync(ct);

        var byCoffee = perReview
            .GroupBy(p => p.CoffeeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.SelectMany(p => p.Names).Distinct().OrderBy(n => n).ToList());

        return rows
            .Select(r => new CoffeeWithStats(
                r.Coffee,
                r.AverageRating,
                r.ReviewCount,
                byCoffee.TryGetValue(r.Coffee.Id, out var tags) ? tags : []))
            .ToList();
    }

    public async Task<IReadOnlyList<CoffeeWithStats>> GetAllAsync(CancellationToken ct = default)
    {
        // Read-only projection: AsNoTracking, newest first (Id is monotonic with
        // insertion; SQLite cannot ORDER BY a DateTimeOffset column).
        var rows = await WithScalarStats(db.Coffees.AsNoTracking().OrderByDescending(c => c.Id))
            .ToListAsync(ct);
        return await AttachTagsAsync(rows, ct);
    }

    public async Task<CoffeeWithStats?> GetWithStatsByIdAsync(int id, CancellationToken ct = default)
    {
        var row = await WithScalarStats(db.Coffees.AsNoTracking().Where(c => c.Id == id))
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        // Reuse the same tag-stitching path as the list query (one source of truth for
        // the dedupe/order semantics).
        return (await AttachTagsAsync([row], ct))[0];
    }

    // Tracked (no AsNoTracking): the returned entity is mutated and saved by the
    // update/photo paths, so it must be attached to the change tracker.
    public async Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Coffees.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        await db.Coffees.AnyAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<string>> GetUsedPhotoPathsAsync(CancellationToken ct = default) =>
        // Projection only — the non-null filter and Select push to SQL, so no Coffee
        // entities are materialized.
        await db.Coffees.AsNoTracking()
            .Where(c => c.PhotoPath != null)
            .Select(c => c.PhotoPath!)
            .ToListAsync(ct);

    public async Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default)
    {
        db.Coffees.Add(coffee);
        await db.SaveChangesAsync(ct);
        return coffee;
    }

    public async Task UpdateAsync(Coffee coffee, CancellationToken ct = default)
    {
        db.Coffees.Update(coffee);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var rows = await db.Coffees
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }
}
