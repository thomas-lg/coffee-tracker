namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Returned to the client on successful register/login: the bearer token to send
/// on subsequent requests, when it expires, and who they are.
/// </summary>
public record AuthResponseDto(
    string Token,
    DateTimeOffset ExpiresAt,
    string UserId,
    string? DisplayName,
    bool IsAdmin);
