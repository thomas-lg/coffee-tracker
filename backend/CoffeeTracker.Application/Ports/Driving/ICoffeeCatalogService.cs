using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for managing the coffee catalog. Driving adapters
/// (e.g. the HTTP controller) depend only on this abstraction.
/// </summary>
public interface ICoffeeCatalogService
{
    /// <summary>Returns the full catalog, newest first.</summary>
    Task<IReadOnlyList<CoffeeResponseDto>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>Returns one coffee, or null if it does not exist.</summary>
    Task<CoffeeResponseDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Creates a coffee from the supplied payload and returns the stored resource.</summary>
    Task<CoffeeResponseDto> CreateAsync(CoffeeCreateDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing coffee. Returns false if no coffee has the given id.</summary>
    Task<bool> UpdateAsync(int id, CoffeeUpdateDto dto, CancellationToken ct = default);

    /// <summary>Deletes a coffee. Returns false if no coffee has the given id.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Stores an uploaded photo and associates it with the coffee. The result
    /// distinguishes a missing coffee and a rejected upload from success.
    /// </summary>
    Task<SetPhotoResult> SetPhotoAsync(int id, Stream content, string? contentType, long length, CancellationToken ct = default);
}

/// <summary>Outcome of <see cref="ICoffeeCatalogService.SetPhotoAsync"/>.</summary>
public enum SetPhotoStatus
{
    Success,
    CoffeeNotFound,
    InvalidContentType,
    TooLarge,
}

/// <summary>
/// Result of attaching a photo. <see cref="Coffee"/> is non-null only when
/// <see cref="Status"/> is <see cref="SetPhotoStatus.Success"/>.
/// </summary>
public sealed record SetPhotoResult(SetPhotoStatus Status, CoffeeResponseDto? Coffee);
