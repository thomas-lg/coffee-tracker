using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Diffs the files on disk (<see cref="IPhotoStorage"/>) against the photos coffees
/// actually reference (<see cref="ICoffeeRepository"/>) to surface and reap orphans.
/// Paths on both sides share the `photos/{name}` shape, so comparison is ordinal set
/// membership.
/// </summary>
public class PhotoAdminService(IPhotoStorage storage, ICoffeeRepository coffees) : IPhotoAdminService
{
    public async Task<IReadOnlyList<PhotoListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var stored = await storage.ListAsync(ct);
        var used = (await coffees.GetUsedPhotoPathsAsync(ct)).ToHashSet(StringComparer.Ordinal);
        return stored.Select(path => new PhotoListItemDto(path, used.Contains(path))).ToList();
    }

    public async Task<PhotoDeleteResultDto> DeleteAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default)
    {
        // Recompute usage at delete time: a photo attached to a coffee between the
        // operator's list and this call must be retained. Only delete a path that is
        // a real stored file AND currently unused.
        var used = (await coffees.GetUsedPhotoPathsAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var stored = (await storage.ListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var deleted = 0;
        var skipped = 0;
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            if (stored.Contains(path) && !used.Contains(path))
            {
                await storage.DeleteAsync(path, ct);
                deleted++;
            }
            else
            {
                skipped++;
            }
        }

        return new PhotoDeleteResultDto(deleted, skipped);
    }
}
