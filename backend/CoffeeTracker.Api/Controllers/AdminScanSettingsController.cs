using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/admin/scan-settings")]
// Admin-only: the engine decides what every scan on the instance costs and how well it
// reads, so it sits behind the same policy as the rest of the admin surface.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public class AdminScanSettingsController(IScanSettingsService settings) : ControllerBase
{
    /// <summary>Returns the engine in force and what this build could switch to.</summary>
    [HttpGet]
    public async Task<ActionResult<ScanSettingsDto>> Get(CancellationToken ct) =>
        Ok(await settings.GetAsync(ct));

    /// <summary>Changes the engine. Takes effect on the next scan; nothing restarts.</summary>
    [HttpPut]
    public async Task<ActionResult<ScanSettingsDto>> Update(UpdateScanSettingsDto request, CancellationToken ct)
    {
        // Model validation has already rejected a missing engine.
        return Ok(await settings.UpdateAsync(request.Engine!.Value, ct));
    }
}
