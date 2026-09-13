using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Driven adapter: reads the whole catalog for an export, and replaces it for a restore.
/// </summary>
public class EfBackupStore(AppDbContext db) : IBackupStore
{
    public async Task<IReadOnlyList<CoffeeWithReviews>> ExportAsync(CancellationToken ct = default)
    {
        // Two queries rather than one per coffee: the reviews are fetched flat with their
        // tags and grouped in memory. An export reads everything by definition, so the
        // N+1 a per-coffee load would cause is exactly what to avoid here.
        var coffees = await db.Coffees
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        var reviews = await db.Reviews
            .AsNoTracking()
            .Include(r => r.Tags)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        var byCoffee = reviews.GroupBy(r => r.CoffeeId).ToDictionary(g => g.Key, g => g.ToList());

        return [.. coffees.Select(c => new CoffeeWithReviews(
            c,
            byCoffee.TryGetValue(c.Id, out var rs) ? rs : []))];
    }

    public async Task<ReplaceResult> ReplaceAllAsync(
        IReadOnlyList<CoffeeWithReviews> coffees,
        CancellationToken ct = default)
    {
        // Tags are reference data seeded at startup; a backup names them, and the names
        // are what stay stable across instances. Matched case-insensitively because the
        // seed's casing is a display choice, not an identity.
        var tagsByName = await db.FlavorTags
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase, ct);

        var warnings = new List<string>();
        var reviewCount = 0;

        // One transaction: a restore that fails halfway would otherwise leave the
        // instance holding neither the old catalog nor the whole of the new one.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Reviews first, then coffees: the review→coffee foreign key cascades, but
        // deleting explicitly keeps the order obvious and the join rows with it.
        await db.Reviews.ExecuteDeleteAsync(ct);
        await db.Coffees.ExecuteDeleteAsync(ct);

        // Coffees first and saved, because Review carries a plain CoffeeId with no
        // navigation property to let EF infer the link — the ids have to exist before the
        // reviews can point at them. Both writes are inside the transaction above.
        foreach (var entry in coffees)
        {
            // Ids come from the database, not the file: a restore creates new rows.
            entry.Coffee.Id = 0;
            db.Coffees.Add(entry.Coffee);
        }

        await db.SaveChangesAsync(ct);

        foreach (var entry in coffees)
        {
            foreach (var review in entry.Reviews)
            {
                review.Id = 0;
                review.CoffeeId = entry.Coffee.Id;
                review.Tags = [.. ResolveTags(review.Tags, tagsByName, warnings)];
                db.Reviews.Add(review);
                reviewCount++;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new ReplaceResult(coffees.Count, reviewCount, warnings);
    }

    /// <summary>
    /// Maps tag names onto the seeded rows. An unknown name is dropped and reported
    /// rather than created: the tag list is a closed set the UI renders as chips, so
    /// inventing one would put a tag on screen that no filter can ever match.
    /// </summary>
    private static IEnumerable<FlavorTag> ResolveTags(
        IEnumerable<FlavorTag> named,
        IReadOnlyDictionary<string, FlavorTag> tagsByName,
        List<string> warnings)
    {
        foreach (var tag in named)
        {
            if (tagsByName.TryGetValue(tag.Name, out var known))
            {
                yield return known;
            }
            else if (!warnings.Contains($"Unknown flavour tag \"{tag.Name}\" was skipped."))
            {
                warnings.Add($"Unknown flavour tag \"{tag.Name}\" was skipped.");
            }
        }
    }
}
