namespace CoffeeTracker.Api;

/// <summary>Named rate-limiter policy keys, shared between registration and the controllers.</summary>
public static class RateLimiterPolicies
{
    /// <summary>Throttles the auth endpoints (register/login) against brute force.</summary>
    public const string Auth = "auth";
}
