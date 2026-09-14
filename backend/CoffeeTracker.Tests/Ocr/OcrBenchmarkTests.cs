using System.Globalization;
using System.Text;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace CoffeeTracker.Tests.Ocr;

/// <summary>
/// Scores the whole snap-to-fill pipeline — engine, then parser — against a fixed corpus,
/// so a change to either can be measured instead of argued about.
///
/// This exists because "the OCR isn't great" was, for the life of the feature, a feeling.
/// The confidence gate in <see cref="CoffeeLabelParser"/> is the one number that was ever
/// measured, and it was measured on a single photograph. Nothing else could be compared
/// before and after, which is exactly what makes an engine swap unarguable in both
/// directions.
///
/// It is a benchmark and a regression test at once: it prints a scorecard, and it fails
/// below <see cref="Floor"/>. The floor is deliberately a little under the current score,
/// so ordinary engine-version drift does not fail the build but a real regression does.
/// </summary>
public sealed class OcrBenchmarkTests(ITestOutputHelper output)
{
    /// <summary>
    /// Minimum overall score, measured at 65.7% on tesseract 5.3.4. The floor sits below
    /// that so a patch release of the engine scoring a point differently does not fail
    /// the build, while a real regression does.
    ///
    /// The orientation fix in this change does not move it, and that is expected: every
    /// rendered fixture is already upright and none carries EXIF, so the corpus cannot
    /// see the bug at all. `TesseractOrientationTests` is where that fix is covered.
    ///
    /// Raise it when a change earns it (that is the point of having it), but never to
    /// paper over a corpus that got easier.
    /// </summary>
    private const double Floor = 0.60;

    /// <summary>
    /// How close two free-text values have to be to count as a half credit. OCR drops a
    /// letter from a name far more often than it invents a different name, and a user
    /// correcting "Kirinyaga A" to "Kirinyaga AA" is in a different world from one facing
    /// an empty field or a line of table grain.
    /// </summary>
    private const double NearThreshold = 0.8;

    [OcrBenchmarkFact]
    public async Task The_pipeline_scores_at_or_above_the_recorded_floor()
    {
        var fixtures = OcrFixtures.All();
        Assert.NotEmpty(fixtures);

        // A generous ceiling, and one process at a time. The production default of 30s
        // bounds a *user's* scan against a hung engine; this loop runs 30 images while
        // the rest of the suite competes for the same cores on a CI runner, and one slow
        // run timing out would degrade to "unavailable" and read as a pipeline failure.
        var ocr = new TesseractCliOcrService(
            Options.Create(new OcrOptions { TimeoutSeconds = 120, MaxConcurrency = 1 }),
            // Not NullLogger: the adapter never throws, so its log is the only place that
            // says *why* a fixture came back unavailable.
            new TestOutputLogger<TesseractCliOcrService>(output));
        var parser = new CoffeeLabelParser();

        List<Scored> scored = [];
        foreach (var fixture in fixtures)
        {
            await using var image = File.OpenRead(fixture.Path);
            var read = await ocr.ReadAsync(image);
            // An engine outage mid-corpus would otherwise read as a catastrophic score
            // and send the next reader hunting for a parser bug.
            Assert.True(
                read.Available,
                $"the engine went unavailable on {fixture.File} — the adapter's own log is "
                + "in this test's output, and says which of start, exit code or timeout it was");

            var parsed = parser.Parse(read);
            scored.Add(new Scored(fixture, Score(fixture.Expected, parsed), parsed));
        }

        var report = Report(scored);
        output.WriteLine(report);
        // Also on disk, so the CI job can put it in the run summary rather than leaving
        // it buried in log output nobody expands.
        await File.WriteAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "ocr-benchmark.md"), report);

        var total = scored.Average(s => s.Fields.Average(f => f.Value.Credit()));
        Assert.True(
            total >= Floor,
            $"OCR pipeline scored {total:P1}, below the {Floor:P1} floor. Scorecard:\n{report}");
    }

    private static IReadOnlyDictionary<string, Outcome> Score(
        OcrExpectation expected, ScannedCoffeeDto actual) =>
        new Dictionary<string, Outcome>
        {
            // Free text: the engine's letter-level mistakes are survivable here.
            ["name"] = Compare(expected.Name, actual.Name, fuzzy: true),
            ["roaster"] = Compare(expected.Roaster, actual.Roaster, fuzzy: true),
            // Closed vocabularies. The app stores a canonical value and filters on it, so
            // "Kenyq" is not a near miss, it is a wrong answer that no filter will match.
            ["origin"] = Compare(expected.Origin, actual.Origin, fuzzy: false),
            ["roastLevel"] = Compare(expected.RoastLevel, actual.RoastLevel, fuzzy: false),
            ["weight"] = Compare(expected.Weight, actual.Weight, fuzzy: false),
        };

    private static Outcome Compare(string? expected, string? actual, bool fuzzy)
    {
        var got = Normalize(actual);
        var want = Normalize(expected);

        if (want.Length == 0)
        {
            // Abstaining was the right answer. Producing something here is the parser
            // inventing a field, which is the failure it is written to avoid.
            return got.Length == 0 ? Outcome.Exact : Outcome.Wrong;
        }

        if (got.Length == 0)
        {
            return Outcome.Missing;
        }

        if (string.Equals(got, want, StringComparison.Ordinal))
        {
            return Outcome.Exact;
        }

        return fuzzy && Similarity(got, want) >= NearThreshold ? Outcome.Near : Outcome.Wrong;
    }

    /// <summary>
    /// Case, accents and punctuation are not what is being measured: the user sees the
    /// value in a text box and the roaster's own capitalisation is a design choice.
    /// </summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var stripped = value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

        var builder = new StringBuilder();
        var lastWasSpace = true; // leading space collapses to nothing
        foreach (var c in stripped)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>1.0 for identical strings, 0.0 for nothing in common (Levenshtein).</summary>
    private static double Similarity(string a, string b)
    {
        var longest = Math.Max(a.Length, b.Length);
        return longest == 0 ? 1.0 : 1.0 - ((double)Distance(a, b) / longest);
    }

    // Two rows rather than the full matrix: the strings are label-sized, but there is no
    // reason to allocate n*m for a number this small.
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private sealed record Scored(
        OcrFixture Fixture,
        IReadOnlyDictionary<string, Outcome> Fields,
        ScannedCoffeeDto Parsed);

    /// <summary>Reads one field off both sides, so the report can show the swap.</summary>
    private static (string? Want, string? Got) Values(Scored scored, string field) => field switch
    {
        "name" => (scored.Fixture.Expected.Name, scored.Parsed.Name),
        "roaster" => (scored.Fixture.Expected.Roaster, scored.Parsed.Roaster),
        "origin" => (scored.Fixture.Expected.Origin, scored.Parsed.Origin),
        "roastLevel" => (scored.Fixture.Expected.RoastLevel, scored.Parsed.RoastLevel),
        _ => (scored.Fixture.Expected.Weight, scored.Parsed.Weight),
    };

    private static string Report(IReadOnlyList<Scored> scored)
    {
        var fields = (string[])["name", "roaster", "origin", "roastLevel", "weight"];
        var report = new StringBuilder();

        var total = scored.Average(s => s.Fields.Average(f => f.Value.Credit()));
        report.AppendLine(CultureInfo.InvariantCulture, $"# OCR benchmark — {total:P1} over {scored.Count} fixtures");
        report.AppendLine();

        report.AppendLine("| field | exact | near | missing | wrong | score |");
        report.AppendLine("| --- | --: | --: | --: | --: | --: |");
        foreach (var field in fields)
        {
            var outcomes = scored.Select(s => s.Fields[field]).ToList();
            report.AppendLine(CultureInfo.InvariantCulture,
                $"| {field} | {outcomes.Count(o => o == Outcome.Exact)} | {outcomes.Count(o => o == Outcome.Near)} " +
                $"| {outcomes.Count(o => o == Outcome.Missing)} | {outcomes.Count(o => o == Outcome.Wrong)} " +
                $"| {outcomes.Average(o => o.Credit()):P0} |");
        }

        report.AppendLine();
        report.AppendLine("| condition | fixtures | score |");
        report.AppendLine("| --- | --: | --: |");
        foreach (var group in scored.GroupBy(s => $"{s.Fixture.Corpus}/{s.Fixture.Condition}").OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"| {group.Key} | {group.Count()} | {group.Average(s => s.Fields.Average(f => f.Value.Credit())):P0} |");
        }

        // The wrong answers by name, because those are the ones a user has to notice and
        // undo — a missing field costs a keystroke, a confident wrong one costs trust.
        var invented = scored
            .SelectMany(s => s.Fields
                .Where(f => f.Value == Outcome.Wrong)
                .Select(f =>
                {
                    var (want, got) = Values(s, f.Key);
                    // Both sides, verbatim: "name was wrong" sends you to re-run the
                    // engine by hand, whereas "wanted X, got Y" usually names the bug.
                    return $"`{s.Fixture.File}` **{f.Key}** — wanted `{want}`, got `{got}`";
                }))
            .ToList();
        if (invented.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(CultureInfo.InvariantCulture, $"## Wrong answers ({invented.Count})");
            report.AppendLine();
            foreach (var line in invented)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
            }
        }

        return report.ToString();
    }
}

/// <summary>What became of one expected field.</summary>
internal enum Outcome
{
    /// <summary>Right, or correctly left null.</summary>
    Exact,

    /// <summary>Close enough that correcting it is a keystroke. Free text only.</summary>
    Near,

    /// <summary>Nothing came back. The user types it; nobody is misled.</summary>
    Missing,

    /// <summary>Something came back and it was wrong. The expensive failure.</summary>
    Wrong,
}

internal static class OutcomeScoring
{
    /// <summary>
    /// Half credit for a near miss, none for a miss or a wrong answer. A wrong answer is
    /// not scored *below* a miss even though it is worse for the user, because the field
    /// tables above already separate the two and double-counting would hide the split.
    /// </summary>
    internal static double Credit(this Outcome outcome) => outcome switch
    {
        Outcome.Exact => 1.0,
        Outcome.Near => 0.5,
        _ => 0.0,
    };
}
