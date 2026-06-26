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
}
