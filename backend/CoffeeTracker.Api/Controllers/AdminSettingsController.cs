using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
// Admin-only: these settings decide who can get in at all, so the policy gates every
// action and a non-admin (even authenticated) gets 403 before reaching here.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public class AdminSettingsController(IAccountSettingsService settings) : ControllerBase
{
    /// <summary>Returns the instance's current account policy.</summary>
    [HttpGet]
    public async Task<ActionResult<AccountSettingsDto>> Get(CancellationToken ct) => Ok(await settings.GetAsync(ct));

    /// <summary>
    /// Updates the account policy. Refuses with 409 when switching local sign-in off
    /// would leave no way into the instance.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<AccountSettingsDto>> Update(UpdateAccountSettingsDto request, CancellationToken ct)
    {
        // Model validation has already rejected a missing field, so both are present.
        var result = await settings.UpdateAsync(
            new AccountSettings(request.LocalLoginEnabled!.Value, request.LocalRegistrationEnabled!.Value),
            ct);
        return result.Status switch
        {
            AccountSettingsStatus.Applied => Ok(result.Settings),
            AccountSettingsStatus.WouldLockEveryoneOut =>
                Problem(statusCode: StatusCodes.Status409Conflict, detail: result.Reason),
            _ => throw new InvalidOperationException($"Unexpected settings status: {result.Status}"),
        };
    }
}
