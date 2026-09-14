using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// A whole instance's catalog, as a file an administrator can keep.
///
/// The point of it is the thing the README warns about: a failed startup migration has
/// no rollback, so a copy taken beforehand is the only way back. A JSON document is a
/// deliberate choice over a database dump, it survives a schema change, and a human can
/// read it to see what they are about to restore.
///
/// Photos are referenced by the path they are stored under and are not carried here. The
/// /photos volume is backed up on its own, and inlining megabytes of images would make
/// the file unreadable and the export a memory problem.
/// </summary>
public record BackupDto(
    int FormatVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyList<BackupCoffeeDto> Coffees)
{
    /// <summary>
    /// Bumped only when the shape changes in a way an older reader would misread. Import
    /// refuses anything it does not recognise rather than guessing at the difference.
    /// </summary>
    public const int CurrentFormatVersion = 1;
}

/// <summary>
/// A coffee and every review of it. Ids are deliberately absent: a restore creates new
/// rows, so carrying the old identifiers would only invite them to collide.
/// </summary>
public record BackupCoffeeDto(
    string Name,
    string Roaster,
    string Origin,
    RoastLevel RoastLevel,
    decimal Price,
    DateOnly DateBought,
    string? PhotoPath,
    string? ShopName,
    string? PurchaseUrl,
    /// <summary>
    /// Kept as-is. On a restore into the same instance it keeps ownership intact; on a
    /// different one it names an account that no longer exists, which leaves the coffee
    /// editable by administrators only, the same rule already applied to rows written
    /// before owner-stamping existed.
    /// </summary>
    string? CreatedByUserId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BackupReviewDto> Reviews);

/// <summary>
/// One dated review. Flavor tags travel by name rather than id: the tag list is seeded
/// reference data, so the names are stable across instances while the ids are not.
/// </summary>
public record BackupReviewDto(
    string UserId,
    int Rating,
    string? Stage,
    string? TastingNotes,
    string? BrewMethod,
    string? Grind,
    string? Ratio,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string> Tags);

/// <summary>What a restore did, so the screen can report it rather than just "done".</summary>
public record ImportResultDto(int Coffees, int Reviews, IReadOnlyList<string> Warnings);

/// <summary>Why a restore was refused, when it was.</summary>
public enum ImportStatus
{
    Restored,

    /// <summary>The file is not a backup, or not one this version can read.</summary>
    UnsupportedFormat,

    /// <summary>The file parses but carries something the domain would reject.</summary>
    Invalid,
}

/// <summary>Outcome of a restore: a result when it ran, a reason when it did not.</summary>
public record ImportOutcome(ImportStatus Status, ImportResultDto? Result, string? Reason);
