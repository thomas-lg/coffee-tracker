using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>Driven (output) port for the seeded flavor-tag set.</summary>
public interface IFlavorTagRepository
{
    /// <summary>All flavor tags, ordered by name.</summary>
    Task<IReadOnlyList<FlavorTag>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolves the given tag ids to tracked entities (unknown ids are ignored),
    /// so they can be attached to a review without inserting new tags.
    /// </summary>
    Task<IReadOnlyList<FlavorTag>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
}
