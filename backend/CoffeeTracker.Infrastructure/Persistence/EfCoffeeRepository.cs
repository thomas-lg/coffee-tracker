using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Driven adapter: EF Core implementation of the coffee repository port.
/// </summary>
public class EfCoffeeRepository(AppDbContext db) : ICoffeeRepository
{
    // Projects a coffee query to include its review aggregates. The average/count
    // are correlated subqueries, so the whole thing is ONE SQL query (no N+1), and
    // AVG over no rows yields null via the nullable cast.
    private IQueryable<CoffeeWithStats> WithStats(IQueryable<Coffee> source) =>
        source.Select(c => new CoffeeWithStats(
            c,
            db.Reviews.Where(r => r.CoffeeId == c.Id).Average(r => (double?)r.Rating),
            db.Reviews.Count(r => r.CoffeeId == c.Id)));

    public async Task<IReadOnlyList<CoffeeWithStats>> GetAllAsync(CancellationToken ct = default)
    {
        // Read-only projection: AsNoTracking, newest first (Id is monotonic with
        // insertion; SQLite cannot ORDER BY a DateTimeOffset column).
        return await WithStats(db.Coffees.AsNoTracking().OrderByDescending(c => c.Id))
            .ToListAsync(ct);
    }

    public async Task<CoffeeWithStats?> GetWithStatsByIdAsync(int id, CancellationToken ct = default) =>
        await WithStats(db.Coffees.AsNoTracking().Where(c => c.Id == id))
            .FirstOrDefaultAsync(ct);

    // Tracked (no AsNoTracking): the returned entity is mutated and saved by the
    // update/photo paths, so it must be attached to the change tracker.
    public async Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Coffees.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        await db.Coffees.AnyAsync(c => c.Id == id, ct);

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
