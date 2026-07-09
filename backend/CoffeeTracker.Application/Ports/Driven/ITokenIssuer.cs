namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>Issues short-lived signed access tokens for authenticated users.</summary>
public interface ITokenIssuer
{
    AccessToken CreateAccessToken(AuthUser user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
