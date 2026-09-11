using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// The account policy as an administrator sees and sets it. The bootstrap bookkeeping
/// the policy also carries is deliberately absent: it is not a knob, and exposing it
/// would invite a client to clear it.
/// </summary>
public record AccountSettingsDto(
    [Required] bool LocalLoginEnabled,
    [Required] bool LocalRegistrationEnabled);
