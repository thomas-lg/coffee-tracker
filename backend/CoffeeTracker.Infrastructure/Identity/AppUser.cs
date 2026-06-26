using Microsoft.AspNetCore.Identity;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// The application's user. Subclasses ASP.NET Identity's <see cref="IdentityUser"/>
/// (a framework/persistence type), which is why it lives in Infrastructure — the
/// Domain stays framework-free and only ever references a user *id string*
/// (e.g. <c>Coffee.CreatedByUserId</c>).
/// </summary>
public class AppUser : IdentityUser
{
    /// <summary>Elevated privileges. Emitted as a JWT claim; the first registered user gets it.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Friendly name shown in the UI (distinct from the login/username).</summary>
    public string? DisplayName { get; set; }
}
