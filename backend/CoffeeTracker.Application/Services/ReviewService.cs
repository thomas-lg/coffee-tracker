using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service implementing the reviews driving port. Enforces the
/// one-review-per-user rule and ownership (owner-only edit; owner-or-admin delete),
/// and maps domain entities to/from DTOs.
/// </summary>
public class ReviewService(
    IReviewRepository reviews,
    IFlavorTagRepository flavorTags,
    ICoffeeRepository coffees,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IReviewService
{
    public async Task<ReviewListResult> GetForCoffeeAsync(int coffeeId, CancellationToken ct = default)
    {
        if (!await coffees.ExistsAsync(coffeeId, ct))
        {
            return new ReviewListResult(ReviewStatus.CoffeeNotFound, null);
        }

        var list = await reviews.GetByCoffeeAsync(coffeeId, ct);
        return new ReviewListResult(ReviewStatus.Success, list.Select(ToDto).ToList());
    }

    public async Task<ReviewResult> CreateAsync(int coffeeId, ReviewCreateDto dto, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (!await coffees.ExistsAsync(coffeeId, ct))
        {
            return new ReviewResult(ReviewStatus.CoffeeNotFound, null);
        }

        if (await reviews.ExistsForUserAsync(coffeeId, userId, ct))
        {
            return new ReviewResult(ReviewStatus.AlreadyReviewed, null);
        }

        var review = new Review
        {
            CoffeeId = coffeeId,
            UserId = userId,
            Rating = dto.Rating,
            TastingNotes = dto.TastingNotes,
            BrewMethod = dto.BrewMethod,
            Grind = dto.Grind,
            Ratio = dto.Ratio,
            CreatedAt = timeProvider.GetUtcNow(),
            Tags = await ResolveTagsAsync(dto.TagIds, ct),
        };

        var saved = await reviews.AddAsync(review, ct);
        return new ReviewResult(ReviewStatus.Success, ToDto(saved));
    }

    public async Task<ReviewResult> UpdateAsync(int coffeeId, int reviewId, ReviewUpdateDto dto, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var review = await reviews.GetByIdAsync(reviewId, ct);
        if (review is null || review.CoffeeId != coffeeId)
        {
            return new ReviewResult(ReviewStatus.ReviewNotFound, null);
        }

        // Editing is owner-only (admins may delete but not rewrite someone's review).
        if (review.UserId != userId)
        {
            return new ReviewResult(ReviewStatus.Forbidden, null);
        }

        review.Rating = dto.Rating;
        review.TastingNotes = dto.TastingNotes;
        review.BrewMethod = dto.BrewMethod;
        review.Grind = dto.Grind;
        review.Ratio = dto.Ratio;
        review.UpdatedAt = timeProvider.GetUtcNow();

        var resolved = await ResolveTagsAsync(dto.TagIds, ct);
        review.Tags.Clear();
        foreach (var tag in resolved)
        {
            review.Tags.Add(tag);
        }

        await reviews.UpdateAsync(review, ct);
        return new ReviewResult(ReviewStatus.Success, ToDto(review));
    }

    public async Task<ReviewStatus> DeleteAsync(int coffeeId, int reviewId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var review = await reviews.GetByIdAsync(reviewId, ct);
        if (review is null || review.CoffeeId != coffeeId)
        {
            return ReviewStatus.ReviewNotFound;
        }

        // Owner or admin (moderation) may delete.
        if (review.UserId != userId && !currentUser.IsAdmin)
        {
            return ReviewStatus.Forbidden;
        }

        await reviews.DeleteAsync(review, ct);
        return ReviewStatus.Success;
    }

    public async Task<IReadOnlyList<FlavorTagDto>> GetFlavorTagsAsync(CancellationToken ct = default)
    {
        var tags = await flavorTags.GetAllAsync(ct);
        return tags.Select(t => new FlavorTagDto(t.Id, t.Name)).ToList();
    }

    private async Task<List<FlavorTag>> ResolveTagsAsync(IReadOnlyList<int>? tagIds, CancellationToken ct)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return [];
        }

        var resolved = await flavorTags.GetByIdsAsync(tagIds, ct);
        return resolved.ToList();
    }

    private string RequireUserId() =>
        currentUser.Id ?? throw new InvalidOperationException("An authenticated user is required.");

    private static ReviewResponseDto ToDto(Review r) => new(
        r.Id,
        r.CoffeeId,
        r.UserId,
        r.Rating,
        r.TastingNotes,
        r.BrewMethod,
        r.Grind,
        r.Ratio,
        r.CreatedAt,
        r.UpdatedAt,
        r.Tags.Select(t => new FlavorTagDto(t.Id, t.Name)).ToList());
}
