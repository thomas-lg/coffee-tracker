using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Driven (output) port for coffee persistence. The application layer depends on
/// this abstraction; an adapter in the infrastructure layer implements it.
/// </summary>
public interface ICoffeeRepository
{
    /// <summary>Returns all coffees, newest first.</summary>
    Task<IReadOnlyList<Coffee>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the coffee with the given id, or null if none exists.</summary>
    Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Persists a new coffee and returns it with its assigned id.</summary>
    Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default);

    /// <summary>Persists changes to an existing coffee.</summary>
    Task UpdateAsync(Coffee coffee, CancellationToken ct = default);

    /// <summary>Removes the coffee with the given id. Returns false if it did not exist.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
