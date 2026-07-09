namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Returned to the client on successful register/login/refresh: a short-lived bearer
/// token to send on subsequent requests, a longer-lived refresh token to obtain the
/// next pair, when each expires, and who they are.
/// </summary>
public record AuthResponseDto(
    string Token,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    string UserId,
    string? DisplayName,
    bool IsAdmin);
