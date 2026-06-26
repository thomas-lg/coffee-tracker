using System.Text.RegularExpressions;
using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Best-effort heuristics turning OCR text into coffee fields. Deliberately
/// conservative: it never throws and leaves a field null when it isn't confident,
/// because the user reviews/corrects everything before saving.
/// </summary>
public partial class CoffeeLabelParser : ICoffeeLabelParser
{
    // Canonical roast levels, longest-match first so "medium-dark" wins over "dark"/"medium".
    private static readonly string[] RoastLevels =
        ["Medium-Dark", "Light-Medium", "Espresso", "Blonde", "Light", "Medium", "Dark"];

    // A pragmatic set of common coffee origins (countries + a few well-known regions).
    private static readonly string[] Origins =
    [
        "Ethiopia", "Kenya", "Colombia", "Brazil", "Guatemala", "Costa Rica", "Panama",
        "Honduras", "El Salvador", "Nicaragua", "Mexico", "Peru", "Bolivia", "Ecuador",
        "Rwanda", "Burundi", "Tanzania", "Uganda", "Yemen", "India", "Indonesia",
        "Sumatra", "Java", "Sulawesi", "Vietnam", "China", "Myanmar", "Papua New Guinea",
        "Jamaica", "Yirgacheffe", "Sidamo", "Guji", "Huila", "Nariño",
    ];

    public ScannedCoffeeDto Parse(string rawText)
    {
        var text = rawText ?? string.Empty;
        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();

        return new ScannedCoffeeDto(
            Name: FindName(lines),
            Roaster: FindRoaster(lines),
            Origin: FindFirst(text, Origins),
            RoastLevel: FindFirst(text, RoastLevels),
            Weight: FindWeight(text));
    }

    // First keyword (case-insensitive, whole-word) that appears anywhere in the text.
    private static string? FindFirst(string text, string[] keywords) =>
        keywords.FirstOrDefault(k => Regex.IsMatch(text, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase));

    private static string? FindWeight(string text)
    {
        var match = WeightRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var amount = match.Groups["amount"].Value.Replace(',', '.');
        var unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kg" => "kg",
            "oz" => "oz",
            "lb" or "lbs" => "lb",
            _ => "g",
        };
        return $"{amount}{unit}";
    }

    // Name: the first "prominent" line (letters, not just a number/weight/roast word).
    private static string? FindName(IReadOnlyList<string> lines) =>
        lines.FirstOrDefault(IsProminent);

    // Roaster: prefer a line that looks like a roaster ("... Roasters/Coffee/Roastery"),
    // otherwise the second prominent line (bags usually lead with the coffee name).
    private static string? FindRoaster(IReadOnlyList<string> lines)
    {
        var byKeyword = lines.FirstOrDefault(l => RoasterKeywordRegex().IsMatch(l));
        if (byKeyword is not null)
        {
            return byKeyword;
        }

        return lines.Where(IsProminent).Skip(1).FirstOrDefault();
    }

    private static bool IsProminent(string line)
    {
        var letters = line.Count(char.IsLetter);
        // Has real words and isn't dominated by a weight/roast token.
        return letters >= 3 && !WeightRegex().IsMatch(line);
    }

    [GeneratedRegex(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|gr|grams|oz|lbs|lb)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeightRegex();

    [GeneratedRegex(@"\b(roasters?|roastery|coffee\s*co\.?|coffee\s*roasters?|coffee)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RoasterKeywordRegex();
}
