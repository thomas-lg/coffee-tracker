using System.Text.Json.Serialization;

namespace CoffeeTracker.Domain;

/// <summary>
/// The roast band of a coffee — a small, closed set. Stored and serialised as its
/// name ("Light"/"Medium"/"Dark"), not an int, via the string enum converter (API +
/// OpenAPI schema) and an EF value conversion. The attribute (not just the global
/// converter) is what makes the generated OpenAPI schema a string enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoastLevel>))]
public enum RoastLevel
{
    Light,
    Medium,
    Dark,
}
