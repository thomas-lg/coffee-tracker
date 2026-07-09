namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// One stored photo: its raw relative <see cref="Path"/> (used to request deletion),
/// a signed <see cref="Url"/> to display it, and whether a coffee references it.
/// </summary>
public record PhotoListItemDto(string Path, string Url, bool Used);

/// <summary>Request body for deleting selected photos by their relative paths.</summary>
public record PhotoDeleteRequestDto(IReadOnlyList<string> Paths);

/// <summary>
/// Outcome of a delete request: how many files were removed versus skipped (a path
/// still referenced by a coffee, already gone, or not a stored file).
/// </summary>
public record PhotoDeleteResultDto(int Deleted, int Skipped);
