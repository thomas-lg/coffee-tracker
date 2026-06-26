using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/flavor-tags")]
[Authorize]
public class FlavorTagsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Lists the available flavor tags.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FlavorTagDto>>> GetFlavorTags(CancellationToken ct) =>
        Ok(await reviews.GetFlavorTagsAsync(ct));
}
