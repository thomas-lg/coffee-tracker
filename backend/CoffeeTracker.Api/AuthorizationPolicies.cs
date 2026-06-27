namespace CoffeeTracker.Api;

/// <summary>Named authorization policy keys, shared between registration and controllers.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires the caller to be an administrator (the token's isAdmin claim).</summary>
    public const string Admin = "admin";
}
