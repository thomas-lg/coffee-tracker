using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Reads and replaces the whole catalog in one go, for export and restore.
///
/// Separate from <see cref="ICoffeeRepository"/> because the shape of the work is
/// different: those methods serve one screen at a time, and composing them here would
/// mean a query per coffee to collect its reviews, then a delete per row. A backup wants
/// the lot in one read and the replacement in one transaction.
/// </summary>
public interface IBackupStore
{
    /// <summary>
    /// Every coffee with its reviews and their tags, ordered oldest first so a restored
    /// instance lists the shelf the way the original did.
    /// </summary>
    Task<IReadOnlyList<CoffeeWithReviews>> ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the catalog with <paramref name="coffees"/>, in one transaction: a
    /// restore that fails halfway must not leave the instance holding neither the old
    /// catalog nor the new one.
    ///
    /// Tags are matched by name against the seeded list; a name that matches nothing is
    /// reported back rather than created, since the tag list is a closed set the UI
    /// renders as chips.
    /// </summary>
    Task<ReplaceResult> ReplaceAllAsync(IReadOnlyList<CoffeeWithReviews> coffees, CancellationToken ct = default);
}

/// <summary>A coffee together with its reviews, which is how a backup travels.</summary>
public sealed record CoffeeWithReviews(Coffee Coffee, IReadOnlyList<Review> Reviews);

/// <summary>How much was written, and anything that was dropped on the way.</summary>
public sealed record ReplaceResult(int Coffees, int Reviews, IReadOnlyList<string> Warnings);
