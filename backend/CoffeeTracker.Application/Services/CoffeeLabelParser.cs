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

        var (name, roaster) = FindNameAndRoaster(lines);
        return new ScannedCoffeeDto(
            Name: name,
            Roaster: roaster,
            Origin: FindEarliest(text, Origins),
            RoastLevel: FindEarliest(text, RoastLevels),
            Weight: FindWeight(text));
    }

    // The keyword that appears EARLIEST in the text (by position, not array order),
    // matched on whole-word boundaries. Plain index scanning avoids compiling a
    // throwaway regex per keyword on every call. Ties go to the earlier array entry,
    // so a longer canonical roast ("Medium-Dark") beats its substring ("Medium").
    private static string? FindEarliest(string text, string[] keywords)
    {
        string? best = null;
        var bestIndex = int.MaxValue;
        foreach (var keyword in keywords)
        {
            var index = IndexOfWord(text, keyword);
            if (index >= 0 && index < bestIndex)
            {
                bestIndex = index;
                best = keyword;
            }
        }
        return best;
    }

    // Case-insensitive whole-word IndexOf: the match must not be flanked by letters
    // or digits (so "Java" doesn't match inside "JavaScript", "India" not "Indiana").
    private static int IndexOfWord(string text, string word)
    {
        var start = 0;
        while (start <= text.Length - word.Length)
        {
            var index = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return -1;
            }

            var leftOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + word.Length;
            var rightOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (leftOk && rightOk)
            {
                return index;
            }

            start = index + 1;
        }
        return -1;
    }

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

    // Best-effort name/roaster from the prominent lines (those with real words;
    // weight-bearing lines stay eligible since bags often print the weight beside
    // the name). Two layouts:
    //  - A line looks like a roaster ("… Roasters/Coffee/Roastery") AND there's
    //    another line to be the name → that line is the roaster, the name comes
    //    from a different line (handles roaster-first bags like "Stumptown … / Hair Bender").
    //  - Otherwise the first prominent line is the name and the roaster is a later
    //    prominent line (or null) — never the same line, so a lone "Blue Bottle
    //    Coffee" is a name, not a duplicated roaster.
    private static (string? Name, string? Roaster) FindNameAndRoaster(IReadOnlyList<string> lines)
    {
        var prominent = lines.Where(IsProminent).ToList();
        if (prominent.Count == 0)
        {
            return (null, null);
        }

        var roasterLine = prominent.FirstOrDefault(l => RoasterKeywordRegex().IsMatch(l));
        if (roasterLine is not null && prominent.Count > 1)
        {
            var name = prominent.First(l => l != roasterLine);
            return (name, roasterLine);
        }

        var firstName = prominent[0];
        var roaster = prominent.Skip(1).FirstOrDefault(l => RoasterKeywordRegex().IsMatch(l))
                      ?? prominent.Skip(1).FirstOrDefault();
        return (firstName, roaster);
    }

    private static bool IsProminent(string line) => line.Count(char.IsLetter) >= 3;

    [GeneratedRegex(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|gr|grams|oz|lbs|lb)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeightRegex();

    [GeneratedRegex(@"\b(roasters?|roastery|coffee\s*co\.?|coffee\s*roasters?|coffee)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RoasterKeywordRegex();
}
