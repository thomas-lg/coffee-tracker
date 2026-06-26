using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/coffees/scan")]
[Authorize]
public class ScanController(ICoffeeScanService scan) : ControllerBase
{
    /// <summary>
    /// Scans a coffee-bag photo and returns the raw OCR text plus best-effort
    /// parsed fields to pre-fill the Add Coffee form. Does not create a coffee.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ScanResponseDto>> Scan(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty image file is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await scan.ScanAsync(stream, file.ContentType, file.Length, ct);

        return result.Status switch
        {
            ScanStatus.Success => Ok(result.Response),
            ScanStatus.InvalidContentType => BadRequest("Unsupported image type. Allowed: JPEG, PNG, WebP."),
            ScanStatus.TooLarge => StatusCode(StatusCodes.Status413PayloadTooLarge, "The uploaded image is too large."),
            ScanStatus.OcrUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, "OCR is not enabled in this environment."),
            _ => throw new InvalidOperationException($"Unexpected scan status: {result.Status}"),
        };
    }
}
