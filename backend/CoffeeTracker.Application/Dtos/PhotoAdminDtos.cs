namespace CoffeeTracker.Application.Dtos;

/// <summary>One stored photo and whether a coffee currently references it.</summary>
public record PhotoListItemDto(string Path, bool Used);

/// <summary>Request body for deleting selected photos by their relative paths.</summary>
public record PhotoDeleteRequestDto(IReadOnlyList<string> Paths);

/// <summary>
/// Outcome of a delete request: how many files were removed versus skipped (a path
/// still referenced by a coffee, already gone, or not a stored file).
/// </summary>
public record PhotoDeleteResultDto(int Deleted, int Skipped);
