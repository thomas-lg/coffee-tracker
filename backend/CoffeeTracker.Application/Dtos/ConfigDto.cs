namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Public client bootstrap config, served anonymously so the SPA can adapt its UI
/// before a user signs in (e.g. show/hide the register option).
/// </summary>
public record ConfigDto(bool RegistrationEnabled);
