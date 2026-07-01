using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// An account is mandatory to use the app, so every catalog endpoint — reads
// included — requires a valid token. Only the auth endpoints are anonymous.
[Authorize]
public class CoffeesController(ICoffeeCatalogService catalog) : ControllerBase
{
    /// <summary>Returns the full coffee catalog.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CoffeeResponseDto>>> GetCoffees(CancellationToken ct)
    {
        var coffees = await catalog.GetCatalogAsync(ct);
        return Ok(coffees);
    }

    /// <summary>Returns a single coffee by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CoffeeResponseDto>> GetCoffee(int id, CancellationToken ct)
    {
        var coffee = await catalog.GetByIdAsync(id, ct);
        return coffee is null ? NotFound() : Ok(coffee);
    }

    /// <summary>Creates a new coffee.</summary>
    [HttpPost]
    public async Task<ActionResult<CoffeeResponseDto>> CreateCoffee(CoffeeCreateDto dto, CancellationToken ct)
    {
        var created = await catalog.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetCoffee), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing coffee (creator or admin only).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCoffee(int id, CoffeeUpdateDto dto, CancellationToken ct)
    {
        var status = await catalog.UpdateAsync(id, dto, ct);
        return status switch
        {
            CatalogWriteStatus.Success => NoContent(),
            CatalogWriteStatus.NotFound => NotFound(),
            CatalogWriteStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unexpected catalog write status: {status}"),
        };
    }

    /// <summary>Deletes a coffee (creator or admin only).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCoffee(int id, CancellationToken ct)
    {
        var status = await catalog.DeleteAsync(id, ct);
        return status switch
        {
            CatalogWriteStatus.Success => NoContent(),
            CatalogWriteStatus.NotFound => NotFound(),
            CatalogWriteStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unexpected catalog write status: {status}"),
        };
    }

    /// <summary>Uploads a photo for a coffee and stores its relative path (creator or admin only).</summary>
    [HttpPost("{id:int}/photo")]
    public async Task<ActionResult<CoffeeResponseDto>> UploadPhoto(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "A non-empty image file is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await catalog.SetPhotoAsync(id, stream, file.ContentType, file.Length, ct);

        return result.Status switch
        {
            SetPhotoStatus.Success => Ok(result.Coffee),
            SetPhotoStatus.CoffeeNotFound => NotFound(),
            SetPhotoStatus.Forbidden => Forbid(),
            SetPhotoStatus.InvalidContentType => Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Unsupported image type. Allowed: JPEG, PNG, WebP."),
            SetPhotoStatus.TooLarge => Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "The uploaded image is too large."),
            _ => throw new InvalidOperationException($"Unexpected set-photo status: {result.Status}"),
        };
    }
}
