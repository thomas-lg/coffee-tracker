using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Export and restore of the whole catalog. Administrator-only: an export carries every
/// account's reviews, and a restore discards what is there.
/// </summary>
public interface IBackupService
{
    Task<BackupDto> ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the catalog with the contents of <paramref name="backup"/>. Destructive
    /// by design, the caller is expected to have confirmed.
    /// </summary>
    Task<ImportOutcome> ImportAsync(BackupDto? backup, CancellationToken ct = default);
}
