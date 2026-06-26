using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/coffees/{coffeeId:int}/reviews")]
[Authorize]
public class ReviewsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Lists a coffee's reviews.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewResponseDto>>> GetReviews(int coffeeId, CancellationToken ct)
    {
        var result = await reviews.GetForCoffeeAsync(coffeeId, ct);
        return result.Status == ReviewStatus.CoffeeNotFound ? NotFound() : Ok(result.Reviews);
    }

    /// <summary>Creates the caller's review for a coffee.</summary>
    [HttpPost]
    public async Task<ActionResult<ReviewResponseDto>> CreateReview(int coffeeId, ReviewCreateDto dto, CancellationToken ct)
    {
        var result = await reviews.CreateAsync(coffeeId, dto, ct);
        return result.Status switch
        {
            ReviewStatus.Success => CreatedAtAction(nameof(GetReviews), new { coffeeId }, result.Review),
            ReviewStatus.CoffeeNotFound => NotFound(),
            ReviewStatus.AlreadyReviewed => Problem(statusCode: StatusCodes.Status409Conflict, detail: "You have already reviewed this coffee."),
            _ => throw new InvalidOperationException($"Unexpected create-review status: {result.Status}"),
        };
    }

    /// <summary>Updates the caller's own review.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReviewResponseDto>> UpdateReview(int coffeeId, int id, ReviewUpdateDto dto, CancellationToken ct)
    {
        var result = await reviews.UpdateAsync(coffeeId, id, dto, ct);
        return result.Status switch
        {
            ReviewStatus.Success => Ok(result.Review),
            ReviewStatus.ReviewNotFound => NotFound(),
            ReviewStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unexpected update-review status: {result.Status}"),
        };
    }

    /// <summary>Deletes a review (owner or admin).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReview(int coffeeId, int id, CancellationToken ct)
    {
        var status = await reviews.DeleteAsync(coffeeId, id, ct);
        return status switch
        {
            ReviewStatus.Success => NoContent(),
            ReviewStatus.ReviewNotFound => NotFound(),
            ReviewStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unexpected delete-review status: {status}"),
        };
    }
}
