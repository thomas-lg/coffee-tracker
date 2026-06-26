using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiterPolicies.Auth)]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Registers a new user (when registration is enabled).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(dto, ct);
        return result.Status switch
        {
            AuthStatus.Success => Ok(result.Response),
            AuthStatus.RegistrationDisabled => StatusCode(StatusCodes.Status403Forbidden, "Registration is disabled."),
            AuthStatus.DuplicateUser => Conflict("An account with that email already exists."),
            AuthStatus.WeakPassword => ValidationProblem(BuildErrors(result.Errors)),
            _ => BadRequest(),
        };
    }

    /// <summary>Authenticates a user and returns a bearer token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto, CancellationToken ct)
    {
        var result = await auth.LoginAsync(dto, ct);
        return result.Status switch
        {
            AuthStatus.Success => Ok(result.Response),
            AuthStatus.LockedOut => StatusCode(StatusCodes.Status423Locked, "Account locked due to repeated failed logins. Try again later."),
            AuthStatus.InvalidCredentials => Unauthorized("Invalid email or password."),
            _ => Unauthorized(),
        };
    }

    private static ModelStateDictionary BuildErrors(IReadOnlyList<string>? errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in errors ?? [])
        {
            modelState.AddModelError("Password", error);
        }
        return modelState;
    }
}
