using System.Security.Claims;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Api.Auth;

/// <summary>
/// Driving-side adapter for <see cref="ICurrentUser"/>: reads the caller's id from
/// the JWT claims on the current request. Token issuance puts the id in the
/// NameIdentifier claim (see TokenService), which JWT bearer maps back on the way in.
/// </summary>
public class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? Id =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
