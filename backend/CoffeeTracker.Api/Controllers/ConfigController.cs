using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

/// <summary>
/// Public client bootstrap config. Anonymous so the SPA can fetch it before the
/// user authenticates (otherwise the global auth fallback policy would 401 it).
/// Depends on the lightweight <see cref="IRegistrationPolicy"/> rather than the full
/// auth use case, so this hot, anonymous endpoint doesn't construct the auth stack.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ConfigController(IRegistrationPolicy registration) : ControllerBase
{
    /// <summary>Returns settings the client needs before sign-in.</summary>
    [HttpGet]
    public ActionResult<ConfigDto> Get() => Ok(new ConfigDto(registration.Enabled));
}
