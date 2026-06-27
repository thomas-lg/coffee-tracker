using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

/// <summary>
/// Public client bootstrap config. Anonymous so the SPA can fetch it before the
/// user authenticates (otherwise the global auth fallback policy would 401 it).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ConfigController(IAuthService auth) : ControllerBase
{
    /// <summary>Returns settings the client needs before sign-in.</summary>
    [HttpGet]
    public ActionResult<ConfigDto> Get() => Ok(new ConfigDto(auth.RegistrationEnabled));
}
