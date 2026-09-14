namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Instance-wide settings an administrator changes at runtime. Exactly one row ever
/// exists, pinned to <see cref="SingletonId"/> so concurrent seeding attempts collide
/// on the primary key instead of creating rivals.
/// </summary>
public class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Whether accounts created in the app may sign in.</summary>
    public bool LocalLoginEnabled { get; set; }

    /// <summary>Whether new accounts may be registered in the app.</summary>
    public bool LocalRegistrationEnabled { get; set; }

    /// <summary>
    /// Whether <see cref="LocalRegistrationEnabled"/> was set by the empty-instance
    /// bootstrap rather than by an administrator. Only bootstrap-opened registration
    /// closes itself once the first account exists.
    /// </summary>
    public bool RegistrationOpenedForBootstrap { get; set; }

    /// <summary>
    /// The OCR engine an administrator chose, or null while they never have.
    /// </summary>
    /// <remarks>
    /// Null is meaningful and is the value an upgraded instance carries: it means "use
    /// whatever the deployment configured", so adding this column cannot change how an
    /// existing instance scans. Stored as the enum's name rather than its number, so the
    /// row stays readable in a SQLite browser and reordering the enum cannot silently
    /// repoint every instance at a different engine.
    /// </remarks>
    public string? OcrEngine { get; set; }
}
