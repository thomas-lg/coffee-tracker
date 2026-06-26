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
}
