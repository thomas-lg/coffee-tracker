using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for administrative photo housekeeping: audit stored photos
/// for orphans and delete a selected set. The HTTP controller depends only on this;
/// admin authorization is enforced at the API boundary, not here.
/// </summary>
public interface IPhotoAdminService
{
    /// <summary>
    /// Lists every stored photo, each flagged used (referenced by a coffee) or unused
    /// (orphaned — e.g. a scan whose coffee was never saved).
    /// </summary>
    Task<IReadOnlyList<PhotoListItemDto>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes the requested photo paths, but only those still unused at delete time —
    /// any a coffee currently references are skipped, so cleanup can't strip a live
    /// coffee of its photo. Best-effort and idempotent.
    /// </summary>
    Task<PhotoDeleteResultDto> DeleteAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default);
}
