namespace CoffeeTracker.Application.Auth;

/// <summary>
/// Claim names shared between token issuance (Infrastructure) and authorization (Api).
/// Kept in the Application layer so neither side has to reference the other for the
/// contract that binds them.
/// </summary>
public static class AppClaims
{
    /// <summary>Admin flag, emitted as "true"/"false".</summary>
    public const string Admin = "isAdmin";
}
