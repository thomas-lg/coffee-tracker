using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Maps the catalog to and from the backup document, and decides what a restore is
/// allowed to accept.
///
/// The validation here is not ceremony. A restore is the one operation that writes rows
/// nobody typed into a form, so the checks the API boundary normally performs have to
/// happen somewhere — a rating of 9 or an empty name would otherwise land in the database
/// and only surface as a broken screen much later.
/// </summary>
public class BackupService(IBackupStore store, TimeProvider timeProvider) : IBackupService
{
    public async Task<BackupDto> ExportAsync(CancellationToken ct = default)
    {
        var coffees = await store.ExportAsync(ct);

        return new BackupDto(
            BackupDto.CurrentFormatVersion,
            timeProvider.GetUtcNow(),
            [.. coffees.Select(ToDto)]);
    }

    public async Task<ImportOutcome> ImportAsync(BackupDto? backup, CancellationToken ct = default)
    {
        if (backup is null || backup.Coffees is null)
        {
            return Refused(ImportStatus.UnsupportedFormat, "That file is not a Coffee Tracker backup.");
        }

        if (backup.FormatVersion != BackupDto.CurrentFormatVersion)
        {
            // Naming both versions matters: the usual cause is a file from a newer
            // instance, and "upgrade first" is only obvious once you can see which is which.
            return Refused(
                ImportStatus.UnsupportedFormat,
                $"That backup is format version {backup.FormatVersion}; this instance reads version " +
                $"{BackupDto.CurrentFormatVersion}.");
        }

        // Validate everything before writing anything. Replacing the catalog and only
        // then discovering row 400 is unusable would leave the instance with neither the
        // old catalog nor a complete new one.
        foreach (var (coffee, index) in backup.Coffees.Select((c, i) => (c, i)))
        {
            if (Invalid(coffee, index) is { } reason)
            {
                return Refused(ImportStatus.Invalid, reason);
            }
        }

        var result = await store.ReplaceAllAsync([.. backup.Coffees.Select(FromDto)], ct);

        return new ImportOutcome(
            ImportStatus.Restored,
            new ImportResultDto(result.Coffees, result.Reviews, result.Warnings),
            null);
    }

    private static ImportOutcome Refused(ImportStatus status, string reason) =>
        new(status, null, reason);

    /// <summary>The same rules the DTOs enforce at the HTTP edge, applied to a file.</summary>
    private static string? Invalid(BackupCoffeeDto coffee, int index)
    {
        var where = $"Coffee {index + 1}";

        if (string.IsNullOrWhiteSpace(coffee.Name)) return $"{where} has no name.";
        if (string.IsNullOrWhiteSpace(coffee.Roaster)) return $"{where} ({coffee.Name}) has no roaster.";
        if (string.IsNullOrWhiteSpace(coffee.Origin)) return $"{where} ({coffee.Name}) has no origin.";
        if (!Enum.IsDefined(coffee.RoastLevel)) return $"{where} ({coffee.Name}) has an unknown roast level.";
        if (coffee.Price < 0) return $"{where} ({coffee.Name}) has a negative price.";

        foreach (var review in coffee.Reviews ?? [])
        {
            if (review.Rating is < 1 or > 5)
            {
                return $"{where} ({coffee.Name}) has a review rated {review.Rating}; ratings are 1 to 5.";
            }

            if (string.IsNullOrWhiteSpace(review.UserId))
            {
                return $"{where} ({coffee.Name}) has a review belonging to nobody.";
            }
        }

        return null;
    }

    private static BackupCoffeeDto ToDto(CoffeeWithReviews entry) => new(
        entry.Coffee.Name,
        entry.Coffee.Roaster,
        entry.Coffee.Origin,
        entry.Coffee.RoastLevel,
        entry.Coffee.Price,
        entry.Coffee.DateBought,
        entry.Coffee.PhotoPath,
        entry.Coffee.ShopName,
        entry.Coffee.PurchaseUrl,
        entry.Coffee.CreatedByUserId,
        entry.Coffee.CreatedAt,
        [.. entry.Reviews.Select(r => new BackupReviewDto(
            r.UserId,
            r.Rating,
            r.Stage,
            r.TastingNotes,
            r.BrewMethod,
            r.Grind,
            r.Ratio,
            r.CreatedAt,
            r.UpdatedAt,
            [.. r.Tags.Select(t => t.Name)]))]);

    private static CoffeeWithReviews FromDto(BackupCoffeeDto dto) => new(
        new Coffee
        {
            Name = dto.Name,
            Roaster = dto.Roaster,
            Origin = dto.Origin,
            RoastLevel = dto.RoastLevel,
            Price = dto.Price,
            DateBought = dto.DateBought,
            PhotoPath = dto.PhotoPath,
            ShopName = dto.ShopName,
            PurchaseUrl = dto.PurchaseUrl,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = dto.CreatedAt,
        },
        [.. (dto.Reviews ?? []).Select(r => new Review
        {
            UserId = r.UserId,
            Rating = r.Rating,
            Stage = r.Stage,
            TastingNotes = r.TastingNotes,
            BrewMethod = r.BrewMethod,
            Grind = r.Grind,
            Ratio = r.Ratio,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            // Resolved by name against the seeded list in the adapter, which is where
            // the tag rows live.
            Tags = [.. r.Tags.Select(name => new FlavorTag { Name = name })],
        })]);
}
