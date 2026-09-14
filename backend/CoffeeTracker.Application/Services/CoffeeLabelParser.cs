using System.Text.RegularExpressions;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Best-effort heuristics turning OCR text into coffee fields. Deliberately
/// conservative: it never throws and leaves a field null when it isn't confident,
/// because the user reviews/corrects everything before saving.
/// </summary>
public partial class CoffeeLabelParser : ICoffeeLabelParser
{
    // Roast keywords, longest-match first so "medium-dark" wins over "dark"/"medium".
    // Both hyphen and space spellings are listed (bags use either); the space form is
    // normalized to the canonical hyphenated value after matching.
    private static readonly string[] RoastLevels =
    [
        "Medium-Dark", "Medium Dark", "Light-Medium", "Light Medium",
        "Espresso", "Blonde", "Light", "Medium", "Dark",
    ];

    /// <summary>
    /// Origins the parser recognises, keyed by the spelling a bag prints and valued by
    /// the one the app stores.
    ///
    /// A bag says "BRÉSIL" or "PERÙ" as readily as "Brazil", and the shelf filter matches
    /// on an exact string, so two spellings of one country split the filter in two. The
    /// alias is what gets searched for and the canonical name is what comes back, the same
    /// way a roast level printed "Medium Dark" is stored as "Medium-Dark".
    ///
    /// Accented and plain spellings are both listed, because OCR drops accents about as
    /// often as it keeps them and the match is a case-insensitive scan, not a collation.
    /// </summary>
    private static readonly (string Printed, string Canonical)[] Origins =
    [
        ("Ethiopia", "Ethiopia"), ("Éthiopie", "Ethiopia"), ("Ethiopie", "Ethiopia"),
        ("Etiopia", "Ethiopia"), ("Etiopía", "Ethiopia"),
        ("Kenya", "Kenya"), ("Kénya", "Kenya"), ("Kenia", "Kenya"),
        ("Colombia", "Colombia"), ("Colombie", "Colombia"),
        ("Brazil", "Brazil"), ("Brésil", "Brazil"), ("Bresil", "Brazil"),
        ("Brasil", "Brazil"), ("Brasile", "Brazil"),
        ("Guatemala", "Guatemala"), ("Guatémala", "Guatemala"),
        ("Costa Rica", "Costa Rica"),
        ("Panama", "Panama"), ("Panamá", "Panama"),
        ("Honduras", "Honduras"),
        ("El Salvador", "El Salvador"), ("Salvador", "El Salvador"),
        ("Nicaragua", "Nicaragua"),
        ("Mexico", "Mexico"), ("México", "Mexico"), ("Mexique", "Mexico"),
        ("Peru", "Peru"), ("Pérou", "Peru"), ("Perou", "Peru"),
        ("Perù", "Peru"), ("Perú", "Peru"),
        ("Bolivia", "Bolivia"), ("Bolivie", "Bolivia"),
        ("Ecuador", "Ecuador"), ("Équateur", "Ecuador"), ("Equateur", "Ecuador"),
        ("Rwanda", "Rwanda"), ("Burundi", "Burundi"),
        ("Tanzania", "Tanzania"), ("Tanzanie", "Tanzania"),
        ("Uganda", "Uganda"), ("Ouganda", "Uganda"),
        ("Yemen", "Yemen"), ("Yémen", "Yemen"),
        ("India", "India"), ("Inde", "India"),
        ("Indonesia", "Indonesia"), ("Indonésie", "Indonesia"),
        ("Sumatra", "Sumatra"), ("Java", "Java"), ("Sulawesi", "Sulawesi"),
        ("Vietnam", "Vietnam"),
        ("China", "China"), ("Chine", "China"),
        ("Myanmar", "Myanmar"), ("Papua New Guinea", "Papua New Guinea"),
        ("Jamaica", "Jamaica"), ("Jamaïque", "Jamaica"),
        ("Yirgacheffe", "Yirgacheffe"), ("Sidamo", "Sidamo"), ("Guji", "Guji"),
        ("Huila", "Huila"), ("Nariño", "Nariño"),
    ];

    /// <summary>The spellings searched for, in the order the table lists them.</summary>
    private static readonly string[] OriginSpellings = [.. Origins.Select(o => o.Printed)];

    /// <summary>
    /// Lines scoring below this mean word confidence (0-100) are treated as noise and
    /// ignored for every field.
    /// </summary>
    /// <remarks>
    /// Measured, not guessed. On a real photo of a bag on a wooden table, tesseract
    /// scored the twelve background/reflection lines between 15.6 and 48.8, and the four
    /// genuinely printed lines between 58.8 and 96.6 - a wide, unambiguous gap. 55 sits
    /// inside it with margin either side. The words the engine is unsure of are exactly
    /// the ones that used to end up as the coffee's name.
    ///
    /// Re-swept later against the whole benchmark corpus, photographs included: 45, 50,
    /// 55 and 60 all score the same, and only 40 and below (noise gets in) or 65 and above
    /// (real lines get dropped) cost anything. 55 sits in the middle of that plateau, so
    /// the number chosen from one photograph turned out to be right for the wrong reason:
    /// there was never a sharp optimum to find.
    /// </remarks>
    public const double DefaultMinConfidence = 55;

    private readonly double _minConfidence;

    public CoffeeLabelParser()
        : this(DefaultMinConfidence)
    {
    }

    public CoffeeLabelParser(double minConfidence) => _minConfidence = minConfidence;

    public ScannedCoffeeDto Parse(string rawText) => Parse(OcrResult.Read(rawText ?? string.Empty));

    public ScannedCoffeeDto Parse(OcrResult ocr)
    {
        var lines = RankLines(ocr?.Lines ?? []);

        var (name, roaster) = FindNameAndRoaster(lines);
        return new ScannedCoffeeDto(
            Name: name,
            Roaster: roaster,
            Origin: CanonicalOrigin(FindKeyword(lines, OriginSpellings)),
            // Normalize "Medium Dark" → "Medium-Dark" so the value is canonical
            // regardless of which spelling the label used.
            RoastLevel: FindKeyword(lines, RoastLevels)?.Replace(' ', '-'),
            // Read from the lines the confidence gate kept, not from RawText: the
            // engine rebuilds RawText from every word it recognised, including the
            // background noise the gate exists to drop. A digit run in a table grain or
            // a reflection would otherwise land as the bag's weight, confidently wrong,
            // which is worse than absent.
            Weight: FindWeight(ConfidentText(ocr?.Lines ?? [])));
    }

    // Picks the keyword that looks most like a *label* rather than prose: it prefers
    // a match on the shortest line (fewest words, a labelled "Ethiopia" / "Medium
    // Roast" line beats a country/roast word buried in a tasting-note sentence like
    // "notes of brazil nut, dark chocolate"). Ties break by earliest line, then
    // earliest position, then array order, so a longer canonical roast
    // ("Medium-Dark") still beats its substring ("Medium") at the same spot.
    // Whole-word matching is plain index scanning (no per-keyword regex compile).
    private static string? FindKeyword(List<string> lines, string[] keywords)
    {
        string? best = null;
        var bestScore = (Words: int.MaxValue, Line: int.MaxValue, Pos: int.MaxValue);
        foreach (var keyword in keywords)
        {
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var pos = IndexOfWord(lines[lineIndex], keyword);
                if (pos < 0)
                {
                    continue;
                }

                var score = (WordCount(lines[lineIndex]), lineIndex, pos);
                if (score.CompareTo(bestScore) < 0)
                {
                    bestScore = score;
                    best = keyword;
                }
            }
        }
        return best;
    }

    /// <summary>Turns the spelling found on the bag into the one the app stores.</summary>
    private static string? CanonicalOrigin(string? printed) =>
        printed is null
            ? null
            : Origins.First(o => string.Equals(o.Printed, printed, StringComparison.OrdinalIgnoreCase))
                .Canonical;

    private static int WordCount(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

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
    //  - A line looks like a roaster ("… Roasters / Coffee Co / Roastery") AND there's
    //    a different line to be the name → that line is the roaster, the name comes
    //    from another line (handles roaster-first bags like "Stumptown … / Hair Bender").
    //  - Otherwise the first prominent line is the name and the roaster is a later
    //    roaster-keyword line (or null), never the same line, and never a weight line,
    //    so a lone "Blue Bottle Coffee" is a name, not a duplicated/junk roaster.
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
            // FirstOrDefault, not First: if every other prominent line is identical to
            // the roaster line, leave the name null rather than throwing.
            var name = prominent.FirstOrDefault(l => l != roasterLine);
            return (name, roasterLine);
        }

        var firstName = prominent[0];
        // Roaster requires POSITIVE evidence - a line that actually says "Roasters",
        // "Roastery", "Coffee Co" etc. There used to be a positional fallback ("any
        // other line that isn't the name or a weight"), which is wrong more often than
        // it is right: on a real bag whose label carries origin, roast and a
        // description but no visible roaster, it promoted the roast line
        // ("TORREFACTION MEDIUM") to roaster. A null the user fills in beats a
        // confident-looking wrong value, which is this parser's stated contract.
        var roaster = prominent.Skip(1).FirstOrDefault(l => RoasterKeywordRegex().IsMatch(l));
        return (firstName, roaster);
    }

    // Orders the lines the field heuristics see, and drops the ones the engine wasn't
    // confident about. This is the difference between "prominent" meaning something and
    // meaning nothing: the old code kept any line with >= 3 letters and then took the
    // FIRST as the name, so a photo whose top edge was a wooden table yielded names like
    // "aren bik re" while the bag's actual "PACIFIC BLEND" sat twelve lines lower.
    //
    // Now: discard low-confidence lines outright, then sort by glyph height descending,
    // because the product name is the biggest thing printed on a bag. Reading order is
    // only the tie-break, and OrderByDescending is stable, so an engine reporting no
    // geometry degrades exactly to the previous ordering rather than to something
    // arbitrary.
    /// <summary>
    /// The lines that clear the confidence gate, in reading order.
    ///
    /// Reading order, not the height ranking RankLines applies: the weight scan takes
    /// the first match, and on a label that prints two of them the one printed first is
    /// the meaningful answer, not the one set in the largest type.
    /// </summary>
    private string ConfidentText(IReadOnlyList<OcrLine> lines) =>
        string.Join(
            '\n',
            lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Where(l => l.Confidence is null || l.Confidence >= _minConfidence)
                .Select(l => l.Text.Trim()));

    private List<string> RankLines(IReadOnlyList<OcrLine> lines)
    {
        var confident = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .Where(l => l.Confidence is null || l.Confidence >= _minConfidence)
            .Select(l => l with { Text = l.Text.Trim() })
            .Where(l => l.Text.Length > 0)
            .ToList();

        return
        [
            .. MergeWrapped(confident)
                .OrderByDescending(l => l.Height ?? 0)
                .Select(l => l.Text),
        ];
    }

    /// <summary>
    /// Rejoins a value the bag printed across two lines.
    ///
    /// A product name set in display type wraps, and the engine reports one line per
    /// visual line, so "Finca El Injerto" arrived as "Finca El" and "Injerto" and the
    /// parser took one fragment as the whole name. Worse, it did not even take the first:
    /// ranking by bounding-box height put "Injerto" above "Finca El", because the
    /// descender on the j makes it measure taller than the identical type beside it.
    ///
    /// Lines are grouped into bands of similar height and then joined where they sit
    /// directly under one another. Both halves are needed: height alone would merge a
    /// roaster into a roast level printed the same size at the other end of the bag, and
    /// adjacency alone would merge a name into the origin line beneath it.
    ///
    /// An engine that reports no geometry gets the previous behaviour, because there is
    /// nothing here to group on.
    /// </summary>
    private static List<OcrLine> MergeWrapped(List<OcrLine> lines)
    {
        if (lines.Any(l => l.Top is null || l.Height is null or 0))
        {
            return lines;
        }

        List<List<OcrLine>> bands = [];
        // Tallest first, so each band is seeded by its most prominent member and a run of
        // slightly-shrinking lines cannot drift a band into a different size of type.
        foreach (var line in lines.OrderByDescending(l => l.Height))
        {
            var band = bands.FirstOrDefault(b => SameBand(b[0], line));
            if (band is null)
            {
                bands.Add([line]);
            }
            else
            {
                band.Add(line);
            }
        }

        List<OcrLine> merged = [];
        foreach (var band in bands)
        {
            OcrLine? open = null;
            foreach (var line in band.OrderBy(l => l.Top))
            {
                if (open is not null && DirectlyBelow(open, line))
                {
                    open = Join(open, line);
                    continue;
                }

                if (open is not null)
                {
                    merged.Add(open);
                }

                open = line;
            }

            if (open is not null)
            {
                merged.Add(open);
            }
        }

        return merged;
    }

    /// <summary>
    /// Whether two lines are the same size of type. The tolerance is wide enough to
    /// absorb a descender (which costs about 25% of the box) and no wider.
    /// </summary>
    private static bool SameBand(OcrLine a, OcrLine b)
    {
        double tallest = Math.Max(a.Height!.Value, b.Height!.Value);
        double shortest = Math.Min(a.Height!.Value, b.Height!.Value);
        return shortest / tallest >= BandTolerance;
    }

    /// <summary>
    /// Whether <paramref name="below"/> is the next line of the same wrapped value: it
    /// starts under the other, and the white band between them is small against the type
    /// size. Lines of a different size sitting between the two are ignored: a speck the
    /// engine read as "e" should not break a name in half.
    /// </summary>
    private static bool DirectlyBelow(OcrLine above, OcrLine below)
    {
        var gap = below.Top!.Value - (above.Top!.Value + above.Height!.Value);
        // Negative when the boxes overlap, which a descender against an ascender does.
        return gap <= MaxLineGap * Math.Max(above.Height!.Value, below.Height!.Value);
    }

    private static OcrLine Join(OcrLine above, OcrLine below) => above with
    {
        Text = $"{above.Text} {below.Text}",
        // The joined line is exactly as trustworthy as its least trustworthy half.
        Confidence = above.Confidence is { } a && below.Confidence is { } b ? Math.Min(a, b) : null,
        Height = Math.Max(above.Height!.Value, below.Height!.Value),
    };

    /// <summary>
    /// How close in height two lines must be to count as the same size of type, as a
    /// ratio. It has to absorb a descender, which costs about a quarter of the box.
    /// </summary>
    /// <remarks>
    /// Swept over the benchmark corpus: 0.85 and 0.80 score 79.3%, 0.75 scores 81.3%,
    /// and 0.70 and 0.65 both score 83.3%. 0.70 is the tighter of the two that tie, so
    /// it is the one that merges the least while still scoring the most.
    /// </remarks>
    private const double BandTolerance = 0.70;

    /// <summary>
    /// The largest white band between two lines that can still be one wrapped value,
    /// relative to the type size.
    /// </summary>
    /// <remarks>
    /// 0.6 was too tight by a hair and it cost four fixtures: a roaster wrapped as
    /// "LA CABRA COFFEE" / "ROASTERS" leaves 13px between 18px lines, and 0.6 allows
    /// 10.8. Swept, 0.6 scores 76.3% and 0.8 scores 81.3%; 1.0 scores the same as 0.8,
    /// so 0.8 is where the gain stops and the tighter value wins the tie.
    /// </remarks>
    private const double MaxLineGap = 0.8;

    // A candidate still has to carry some letters. Deliberately loose: OCR mangles real
    // labels (the bag above came out as "ACIFIC BLEND", missing its P), so strictness
    // here would reject good text. RankLines is what excludes junk now.
    /// <summary>
    /// Letters a line needs before it can be a name or a roaster.
    /// </summary>
    /// <remarks>
    /// It was 3, which let through exactly the debris a photograph produces: "lam" beat
    /// "LA LIBERTAD" on the Lugat bag, "INA" beat "L'Original", "ry." led the Caffe Mauro
    /// one. Each is a scrap of a larger glyph with a tall bounding box, so the height
    /// ranking crowned it. Swept over the corpus, 4 takes the name field from 55% to 58%
    /// and handheld photographs from 52% to 56%; 5 and 6 score the same, so 4 is the
    /// loosest value that wins and the least likely to throw away a genuinely short name.
    /// </remarks>
    private const int MinNameLetters = 4;

    private static bool IsProminent(string line) => line.Count(char.IsLetter) >= MinNameLetters;

    [GeneratedRegex(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|gr|grams|oz|lbs|lb)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeightRegex();

    // Roaster-indicating phrases. Deliberately NOT bare "coffee", because that
    // over-triggers
    // on ordinary product lines like "Ethiopia Coffee" and inverts name/roaster.
    // Beyond English, the words a bag actually prints for "we roast this": French
    // torréfacteur (NOT torréfaction, which is the process and sits on the roast line),
    // Italian torrefazione, German Rösterei, Spanish tostaduría. Accented and plain
    // spellings both, because OCR drops accents as often as it keeps them.
    [GeneratedRegex(
        @"\b(roasters?|roastery|coffee\s*co\.?|coffee\s*roasters?|roasting\s*co\.?"
        + @"|torr[eé]facteurs?|torrefazione|br[uû]lerie|r[oö]sterei|kaffeer[oö]sterei"
        + @"|tostadur[ií]a|tostadores)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex RoasterKeywordRegex();
}
