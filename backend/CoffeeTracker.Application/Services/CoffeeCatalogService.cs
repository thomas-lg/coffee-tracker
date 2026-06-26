using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service implementing the catalog driving port. Orchestrates the
/// repository and photo-storage ports and maps domain entities to/from DTOs.
/// </summary>
public class CoffeeCatalogService(
    ICoffeeRepository repository,
    IPhotoStorage photoStorage,
    TimeProvider timeProvider) : ICoffeeCatalogService
{
    public async Task<IReadOnlyList<CoffeeResponseDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var coffees = await repository.GetAllAsync(ct);
        return coffees.Select(ToDto).ToList();
    }

    public async Task<CoffeeResponseDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var coffee = await repository.GetByIdAsync(id, ct);
        return coffee is null ? null : ToDto(coffee);
    }

    public async Task<CoffeeResponseDto> CreateAsync(CoffeeCreateDto dto, CancellationToken ct = default)
    {
        var coffee = new Coffee
        {
            Name = dto.Name,
            Roaster = dto.Roaster,
            Origin = dto.Origin,
            RoastLevel = dto.RoastLevel,
            Price = dto.Price,
            DateBought = dto.DateBought,
            ShopName = dto.ShopName,
            PurchaseUrl = dto.PurchaseUrl,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var saved = await repository.AddAsync(coffee, ct);
        return ToDto(saved);
    }

    public async Task<bool> UpdateAsync(int id, CoffeeUpdateDto dto, CancellationToken ct = default)
    {
        var coffee = await repository.GetByIdAsync(id, ct);
        if (coffee is null)
        {
            return false;
        }

        coffee.Name = dto.Name;
        coffee.Roaster = dto.Roaster;
        coffee.Origin = dto.Origin;
        coffee.RoastLevel = dto.RoastLevel;
        coffee.Price = dto.Price;
        coffee.DateBought = dto.DateBought;
        coffee.ShopName = dto.ShopName;
        coffee.PurchaseUrl = dto.PurchaseUrl;

        await repository.UpdateAsync(coffee, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var coffee = await repository.GetByIdAsync(id, ct);
        if (coffee is null)
        {
            return false;
        }

        await repository.DeleteAsync(id, ct);

        // Remove the associated photo so deleting a coffee doesn't orphan its file.
        if (coffee.PhotoPath is not null)
        {
            await photoStorage.DeleteAsync(coffee.PhotoPath, ct);
        }

        return true;
    }

    public async Task<SetPhotoResult> SetPhotoAsync(int id, Stream content, string? contentType, long length, CancellationToken ct = default)
    {
        var coffee = await repository.GetByIdAsync(id, ct);
        if (coffee is null)
        {
            return new SetPhotoResult(SetPhotoStatus.CoffeeNotFound, null);
        }

        var stored = await photoStorage.SaveAsync(content, contentType, length, ct);
        if (stored.Status != PhotoStorageStatus.Stored)
        {
            return new SetPhotoResult(MapRejection(stored.Status), null);
        }

        var previousPath = coffee.PhotoPath;
        coffee.PhotoPath = stored.RelativePath;

        try
        {
            await repository.UpdateAsync(coffee, ct);
        }
        catch
        {
            // The new file is already on disk but the path didn't get persisted;
            // delete it so a failed update doesn't leave an unreferenced orphan.
            await photoStorage.DeleteAsync(stored.RelativePath, ct);
            throw;
        }

        // Replacing an existing photo: drop the previous file so it doesn't linger.
        // (Stored names are random GUIDs, so the new path is always distinct.)
        if (previousPath is not null)
        {
            await photoStorage.DeleteAsync(previousPath, ct);
        }

        return new SetPhotoResult(SetPhotoStatus.Success, ToDto(coffee));
    }

    private static SetPhotoStatus MapRejection(PhotoStorageStatus status) => status switch
    {
        PhotoStorageStatus.InvalidContentType => SetPhotoStatus.InvalidContentType,
        PhotoStorageStatus.TooLarge => SetPhotoStatus.TooLarge,
        _ => SetPhotoStatus.InvalidContentType,
    };

    private static CoffeeResponseDto ToDto(Coffee c) => new(
        c.Id,
        c.Name,
        c.Roaster,
        c.Origin,
        c.RoastLevel,
        c.Price,
        c.DateBought,
        c.PhotoPath,
        c.ShopName,
        c.PurchaseUrl,
        c.CreatedAt);
}
