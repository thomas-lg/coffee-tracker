namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>JWT signing/validation settings, bound from the <c>Jwt</c> config section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 signing key. Required and validated at startup — never defaulted.</summary>
    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "coffee-tracker";

    public string Audience { get; set; } = "coffee-tracker";

    /// <summary>
    /// Access-token lifetime in minutes. Kept short — a stolen token expires quickly;
    /// sessions stay alive via the rotating refresh token, not a long-lived access token.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token lifetime in days (the effective session length).</summary>
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Minimum key length for HS256 (256 bits). Used by the startup fail-fast check.</summary>
    public const int MinKeyBytes = 32;
}
