using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// A coffee together with its review aggregates, returned from read paths so the
/// catalog can surface average rating / review count without an N+1 query and
/// without polluting the domain entity with read-model concerns.
/// </summary>
public sealed record CoffeeWithStats(Coffee Coffee, double? AverageRating, int ReviewCount);

/// <summary>
/// Driven (output) port for coffee persistence. The application layer depends on
/// this abstraction; an adapter in the infrastructure layer implements it.
/// </summary>
public interface ICoffeeRepository
{
    /// <summary>Returns all coffees (with review aggregates), newest first.</summary>
    Task<IReadOnlyList<CoffeeWithStats>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns one coffee with its review aggregates, or null if none exists.</summary>
    Task<CoffeeWithStats?> GetWithStatsByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns the coffee with the given id (tracked, for writes), or null.</summary>
    Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns whether a coffee with the given id exists.</summary>
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);

    /// <summary>Persists a new coffee and returns it with its assigned id.</summary>
    Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default);

    /// <summary>Persists changes to an existing coffee.</summary>
    Task UpdateAsync(Coffee coffee, CancellationToken ct = default);

    /// <summary>Removes the coffee with the given id. Returns false if it did not exist.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
