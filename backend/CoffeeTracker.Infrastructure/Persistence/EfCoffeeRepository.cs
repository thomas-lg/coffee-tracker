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
        // Newest first. Ordered by Id (monotonic with insertion) because SQLite
        // cannot ORDER BY a DateTimeOffset column.
        return await db.Coffees
            .OrderByDescending(c => c.Id)
            .ToListAsync(ct);
    }
}
