using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>Driven (output) port for review persistence.</summary>
public interface IReviewRepository
{
    /// <summary>All reviews for a coffee (with their tags), newest first.</summary>
    Task<IReadOnlyList<Review>> GetByCoffeeAsync(int coffeeId, CancellationToken ct = default);

    /// <summary>A single review by id (with its tags), tracked for mutation; null if missing.</summary>
    Task<Review?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Whether the user already has a review for the coffee.</summary>
    Task<bool> ExistsForUserAsync(int coffeeId, string userId, CancellationToken ct = default);

    Task<Review> AddAsync(Review review, CancellationToken ct = default);

    Task UpdateAsync(Review review, CancellationToken ct = default);

    Task DeleteAsync(Review review, CancellationToken ct = default);
}
