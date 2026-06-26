using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Driven adapter: EF Core implementation of the coffee repository port.
/// </summary>
public class EfCoffeeRepository(AppDbContext db) : ICoffeeRepository
{
    public async Task<IReadOnlyList<Coffee>> GetAllAsync(CancellationToken ct = default)
    {
        // AsNoTracking: this is a read-only query whose results are mapped to
        // DTOs and discarded, so the change tracker's per-entity snapshots are
        // pure overhead.
        // Newest first. Ordered by Id (monotonic with insertion) because SQLite
        // cannot ORDER BY a DateTimeOffset column.
        return await db.Coffees
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .ToListAsync(ct);
    }

    // Tracked (no AsNoTracking): the returned entity is mutated and saved by the
    // update/photo paths, so it must be attached to the change tracker.
    public async Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Coffees.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default)
    {
        db.Coffees.Add(coffee);
        await db.SaveChangesAsync(ct);
        return coffee;
    }

    public async Task UpdateAsync(Coffee coffee, CancellationToken ct = default)
    {
        // Idempotent whether `coffee` is already tracked (from GetByIdAsync) or
        // detached: Update marks it modified either way.
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
