using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// The account policy as an administrator reads it back. The bootstrap bookkeeping the
/// policy also carries is deliberately absent: it is not a knob, and exposing it would
/// invite a client to clear it.
/// </summary>
public record AccountSettingsDto(bool LocalLoginEnabled, bool LocalRegistrationEnabled);

/// <summary>
/// The same two flags as a request body, nullable on purpose, and separate from the
/// response for that reason alone.
///
/// On a non-nullable bool, [Required] is satisfied by `false`, so a body that omits a
/// field binds it to false and passes validation, and this endpoint can switch off the
/// only way into the instance. Nullable makes an omission a 400 instead of a silent,
/// destructive default. Keeping it off the response type stops that nullability from
/// leaking into a contract where both values are always present.
/// </summary>
public record UpdateAccountSettingsDto(
    [Required] bool? LocalLoginEnabled,
    [Required] bool? LocalRegistrationEnabled);
