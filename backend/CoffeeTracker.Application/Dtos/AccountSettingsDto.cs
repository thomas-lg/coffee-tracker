using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// The account policy as an administrator sees and sets it. The bootstrap bookkeeping
/// the policy also carries is deliberately absent: it is not a knob, and exposing it
/// would invite a client to clear it.
///
/// Nullable on purpose. On a non-nullable bool, [Required] is satisfied by `false`, so
/// a body that simply omits a field binds it to false and passes validation — and this
/// endpoint can switch off the only way into the instance. Nullable makes an omission
/// a 400 instead of a silent, destructive default.
/// </summary>
public record AccountSettingsDto(
    [Required] bool? LocalLoginEnabled,
    [Required] bool? LocalRegistrationEnabled);
