using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>Driven adapter: EF Core implementation of the flavor-tag repository port.</summary>
public class EfFlavorTagRepository(AppDbContext db) : IFlavorTagRepository
{
    public async Task<IReadOnlyList<FlavorTag>> GetAllAsync(CancellationToken ct = default) =>
        await db.FlavorTags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    // Tracked (no AsNoTracking): the resolved tags are attached to a review, so EF
    // must treat them as existing rows (create join entries) rather than new tags.
    public async Task<IReadOnlyList<FlavorTag>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default) =>
        await db.FlavorTags.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
}
