using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Domain;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Application service implementing the reviews driving port. A user may rate the
/// same coffee multiple times over its life (each POST is a new dated entry);
/// enforces ownership (owner-only edit; owner-or-admin delete) and maps domain
/// entities to/from DTOs.
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

    public async Task<ReviewResult> GetByIdAsync(int coffeeId, int reviewId, CancellationToken ct = default)
    {
        var review = await reviews.GetByIdAsync(reviewId, ct);
        return review is null || review.CoffeeId != coffeeId
            ? new ReviewResult(ReviewStatus.ReviewNotFound, null)
            : new ReviewResult(ReviewStatus.Success, ToDto(review));
    }

    public async Task<ReviewResult> CreateAsync(int coffeeId, ReviewCreateDto dto, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (!await coffees.ExistsAsync(coffeeId, ct))
        {
            return new ReviewResult(ReviewStatus.CoffeeNotFound, null);
        }

        var tags = await ResolveTagsAsync(dto.TagIds, ct);
        if (tags is null)
        {
            return new ReviewResult(ReviewStatus.InvalidTags, null);
        }

        // A user may rate the same coffee repeatedly over its life — each POST is a
        // new dated entry, no duplicate check.
        var review = new Review
        {
            CoffeeId = coffeeId,
            UserId = userId,
            Rating = dto.Rating,
            Stage = dto.Stage,
            TastingNotes = dto.TastingNotes,
            BrewMethod = dto.BrewMethod,
            Grind = dto.Grind,
            Ratio = dto.Ratio,
            CreatedAt = timeProvider.GetUtcNow(),
            Tags = tags,
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
        if (!review.IsEditableBy(userId))
        {
            return new ReviewResult(ReviewStatus.Forbidden, null);
        }

        var resolved = await ResolveTagsAsync(dto.TagIds, ct);
        if (resolved is null)
        {
            return new ReviewResult(ReviewStatus.InvalidTags, null);
        }

        review.Rating = dto.Rating;
        review.Stage = dto.Stage;
        review.TastingNotes = dto.TastingNotes;
        review.BrewMethod = dto.BrewMethod;
        review.Grind = dto.Grind;
        review.Ratio = dto.Ratio;
        review.UpdatedAt = timeProvider.GetUtcNow();

        // Full replace (PUT semantics, consistent with the coffee update): the tag set
        // becomes exactly dto.TagIds — null/empty clears all tags.
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
        if (!review.IsDeletableBy(userId, currentUser.IsAdmin))
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

    /// <summary>
    /// Resolves the requested tag ids to entities. Returns null if any id doesn't
    /// exist (so the caller can reject with "invalid tags" rather than silently
    /// dropping them), or an empty list when no tags were requested.
    /// </summary>
    private async Task<List<FlavorTag>?> ResolveTagsAsync(IReadOnlyList<int>? tagIds, CancellationToken ct)
    {
        var requested = tagIds?.Distinct().ToList() ?? [];
        if (requested.Count == 0)
        {
            return [];
        }

        var resolved = await flavorTags.GetByIdsAsync(requested, ct);
        return resolved.Count == requested.Count ? resolved.ToList() : null;
    }

    private string RequireUserId() =>
        currentUser.Id ?? throw new InvalidOperationException("An authenticated user is required.");

    private static ReviewResponseDto ToDto(Review r) => new(
        r.Id,
        r.CoffeeId,
        r.UserId,
        r.Rating,
        r.Stage,
        r.TastingNotes,
        r.BrewMethod,
        r.Grind,
        r.Ratio,
        r.CreatedAt,
        r.UpdatedAt,
        r.Tags.Select(t => new FlavorTagDto(t.Id, t.Name)).ToList());
}
