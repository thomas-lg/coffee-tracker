namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Driven (output) port exposing the authenticated caller's identity to the
/// application layer, without coupling it to ASP.NET's HttpContext. Implemented in
/// the Api layer over the request's claims; trivially faked in tests.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or null when the request is anonymous.</summary>
    string? Id { get; }
}
