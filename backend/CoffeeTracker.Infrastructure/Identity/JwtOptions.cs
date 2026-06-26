namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>JWT signing/validation settings, bound from the <c>Jwt</c> config section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 signing key. Required and validated at startup — never defaulted.</summary>
    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "coffee-tracker";

    public string Audience { get; set; } = "coffee-tracker";

    /// <summary>Token lifetime. Default 7 days (no refresh tokens in M3; users re-login).</summary>
    public int LifetimeMinutes { get; set; } = 60 * 24 * 7;

    /// <summary>Minimum key length for HS256 (256 bits). Used by the startup fail-fast check.</summary>
    public const int MinKeyBytes = 32;
}
