using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for managing coffee reviews and reading flavor tags.
/// Driving adapters (the HTTP controllers) depend only on this abstraction.
/// </summary>
public interface IReviewService
{
    /// <summary>Lists a coffee's reviews. <see cref="ReviewListResult.Status"/> is CoffeeNotFound if the coffee is missing.</summary>
    Task<ReviewListResult> GetForCoffeeAsync(int coffeeId, CancellationToken ct = default);

    /// <summary>Returns a single review (scoped to the coffee), or ReviewNotFound.</summary>
    Task<ReviewResult> GetByIdAsync(int coffeeId, int reviewId, CancellationToken ct = default);

    Task<ReviewResult> CreateAsync(int coffeeId, ReviewCreateDto dto, CancellationToken ct = default);

    /// <summary>Updates a review the caller owns.</summary>
    Task<ReviewResult> UpdateAsync(int coffeeId, int reviewId, ReviewUpdateDto dto, CancellationToken ct = default);

    /// <summary>Deletes a review (owner or admin).</summary>
    Task<ReviewStatus> DeleteAsync(int coffeeId, int reviewId, CancellationToken ct = default);

    Task<IReadOnlyList<FlavorTagDto>> GetFlavorTagsAsync(CancellationToken ct = default);
}

/// <summary>Outcome of a review operation. Expected failures are data, not exceptions.</summary>
public enum ReviewStatus
{
    Success,
    CoffeeNotFound,
    ReviewNotFound,
    Forbidden,
    /// <summary>One or more referenced flavor-tag ids don't exist.</summary>
    InvalidTags,
}

public sealed record ReviewResult(ReviewStatus Status, ReviewResponseDto? Review);

public sealed record ReviewListResult(ReviewStatus Status, IReadOnlyList<ReviewResponseDto>? Reviews);
