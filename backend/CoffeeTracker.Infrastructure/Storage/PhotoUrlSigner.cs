using System.Security.Cryptography;
using System.Text;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Storage;

/// <summary>
/// HMAC-SHA256 signer for photo URLs. The signing key is derived from the JWT signing
/// key (so no extra secret is required) but domain-separated, so a photo URL can never
/// be repurposed as an auth token or vice versa.
/// </summary>
public sealed class PhotoUrlSigner : IPhotoUrlSigner
{
    private const string PublicPrefix = "photos";

    private readonly byte[] _key;
    private readonly TimeProvider _clock;
    private readonly int _lifetimeMinutes;

    public PhotoUrlSigner(IOptions<JwtOptions> jwt, IOptions<PhotoStorageOptions> storage, TimeProvider clock)
    {
        _key = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwt.Value.Key),
            "coffee-tracker/photo-url-signing/v1"u8.ToArray());
        _clock = clock;
        _lifetimeMinutes = storage.Value.SignedUrlLifetimeMinutes;
    }

    public string? Sign(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var fileName = relativePath.StartsWith(PublicPrefix + "/", StringComparison.Ordinal)
            ? relativePath[(PublicPrefix.Length + 1)..]
            : relativePath;

        var exp = _clock.GetUtcNow().AddMinutes(_lifetimeMinutes).ToUnixTimeSeconds();
        var sig = Compute(fileName, exp);
        return $"/{PublicPrefix}/{Uri.EscapeDataString(fileName)}?exp={exp}&sig={sig}";
    }

    public bool Validate(string fileName, string? exp, string? sig)
    {
        if (string.IsNullOrEmpty(sig) || !long.TryParse(exp, out var expiresAt))
        {
            return false;
        }

        if (_clock.GetUtcNow().ToUnixTimeSeconds() > expiresAt)
        {
            return false;
        }

        var expected = Compute(fileName, expiresAt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sig),
            Encoding.UTF8.GetBytes(expected));
    }

    private string Compute(string fileName, long exp)
    {
        using var hmac = new HMACSHA256(_key);
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{fileName}\n{exp}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
