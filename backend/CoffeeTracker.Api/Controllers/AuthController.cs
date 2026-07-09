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
            AuthStatus.RegistrationDisabled => Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Registration is disabled."),
            AuthStatus.DuplicateUser => Problem(statusCode: StatusCodes.Status409Conflict, detail: "An account with that email already exists."),
            AuthStatus.WeakPassword => ValidationProblem(BuildErrors("Password", result.Errors)),
            AuthStatus.InvalidInput => ValidationProblem(BuildErrors(string.Empty, result.Errors)),
            // Login-only statuses can't occur here; fail loudly rather than mis-mapping.
            _ => throw new InvalidOperationException($"Unexpected register status: {result.Status}"),
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
            AuthStatus.LockedOut => Problem(statusCode: StatusCodes.Status423Locked, detail: "Account locked due to repeated failed logins. Try again later."),
            AuthStatus.InvalidCredentials => Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid email or password."),
            _ => throw new InvalidOperationException($"Unexpected login status: {result.Status}"),
        };
    }

    /// <summary>Exchanges a valid refresh token for a fresh access + refresh token pair.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto dto, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(dto.RefreshToken, ct);
        return result.Status switch
        {
            AuthStatus.Success => Ok(result.Response),
            AuthStatus.InvalidRefreshToken => Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid or expired refresh token."),
            _ => throw new InvalidOperationException($"Unexpected refresh status: {result.Status}"),
        };
    }

    /// <summary>Revokes the presented refresh token (sign-out). Idempotent.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto, CancellationToken ct)
    {
        await auth.LogoutAsync(dto.RefreshToken, ct);
        return NoContent();
    }

    private static ModelStateDictionary BuildErrors(string key, IReadOnlyList<string>? errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in errors ?? [])
        {
            modelState.AddModelError(key, error);
        }
        return modelState;
    }
}
