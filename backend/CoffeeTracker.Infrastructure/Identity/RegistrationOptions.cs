namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Whether open registration is allowed. Bound from the flat <c>REGISTRATION_ENABLED</c>
/// env var / config key (default false), so a public instance is closed unless opted in.
/// </summary>
public class RegistrationOptions
{
    public bool Enabled { get; set; }
}
