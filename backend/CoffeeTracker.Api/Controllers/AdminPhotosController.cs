using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/admin/photos")]
// Admin-only housekeeping: audit stored photos and delete orphans. The policy gates
// every action, so a non-admin (even authenticated) gets 403 before reaching here.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public class AdminPhotosController(IPhotoAdminService photos) : ControllerBase
{
    /// <summary>Lists every stored photo, each flagged used or unused (orphaned).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PhotoListItemDto>>> ListPhotos(CancellationToken ct)
        => Ok(await photos.ListAsync(ct));

    /// <summary>
    /// Deletes the selected photo paths. Paths a coffee still references are skipped
    /// (not deleted); the response reports how many were removed versus skipped.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult<PhotoDeleteResultDto>> DeletePhotos(PhotoDeleteRequestDto request, CancellationToken ct)
    {
        if (request?.Paths is null || request.Paths.Count == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "At least one photo path is required.");
        }

        return Ok(await photos.DeleteAsync(request.Paths, ct));
    }
}
