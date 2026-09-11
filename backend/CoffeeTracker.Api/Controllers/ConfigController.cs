using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

/// <summary>
/// Public client bootstrap config. Anonymous so the SPA can fetch it before the
/// user authenticates (otherwise the global auth fallback policy would 401 it).
/// Depends on the lightweight account-policy and provider ports rather than the full
/// auth use case, so this hot, anonymous endpoint doesn't construct the auth stack.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ConfigController(IAccountPolicy accountPolicy, IExternalIdentityProvider provider) : ControllerBase
{
    /// <summary>Returns settings the client needs before sign-in.</summary>
    [HttpGet]
    public async Task<ActionResult<ConfigDto>> Get(CancellationToken ct)
    {
        var policy = await accountPolicy.GetAsync(ct);
        var info = await provider.GetClientInfoAsync(ct);
        return Ok(new ConfigDto(
            policy.LocalLoginEnabled,
            policy.LocalRegistrationEnabled,
            info is not null,
            info is null ? null : new OidcClientConfigDto(info.Authority, info.ClientId, info.Scopes)));
    }
}
