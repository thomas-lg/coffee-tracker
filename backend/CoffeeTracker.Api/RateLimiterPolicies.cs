namespace CoffeeTracker.Api;

/// <summary>
/// The named rate-limiter policies and the budget each one grants a client per minute.
///
/// Both halves live here because both are shared. The name has to be a compile-time
/// constant — it is what a controller's <c>[EnableRateLimiting]</c> attribute names —
/// and the budget is read by the registration in Program.cs and asserted by the tests,
/// neither of which should be restating a number the other owns.
/// </summary>
public static class RateLimiterPolicies
{
    /// <summary>Throttles the auth endpoints (register/login) against brute force.</summary>
    public const string Auth = "auth";

    /// <summary>Throttles anonymous endpoints the client may reach before signing in.</summary>
    public const string Public = "public";

    /// <summary>Throttles label scanning, which occupies an OCR process per request.</summary>
    public const string Scan = "scan";

    /// <summary>
    /// Low: the only honest reason to reach these ten times in a minute is a script.
    /// Account-level lockout (Identity) is the second layer.
    /// </summary>
    public const int AuthPermitsPerMinute = 10;

    /// <summary>
    /// Generous: the client legitimately asks for the config on boot and again when it
    /// reaches the sign-in screen, and a browser reload repeats both.
    /// </summary>
    public const int PublicPermitsPerMinute = 60;

    /// <summary>
    /// A scan holds an OCR process for as long as the configured timeout, so this is
    /// what stops one client queueing enough of them to starve everyone else's.
    /// </summary>
    public const int ScanPermitsPerMinute = 12;
}
