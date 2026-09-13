using System.Text.Json;
using System.Text.Json.Serialization;
using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests.Ocr;

/// <summary>One benchmark image and what the pipeline is expected to make of it.</summary>
/// <param name="File">Filename, relative to the corpus directory holding the manifest.</param>
/// <param name="Condition">
/// How the label was photographed (<c>flat</c>, <c>glare</c>, <c>dark</c>…). Scores are
/// reported per condition, because that is where a change shows its shape: preprocessing
/// that helps a dark bag and hurts a glared one is a wash in the total and obvious here.
/// </param>
public sealed record OcrFixture(string File, string Condition, OcrExpectation Expected)
{
    /// <summary>Absolute path, filled in when the manifest is loaded.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Which corpus it came from — <c>synthetic</c> or <c>real</c>.</summary>
    public string Corpus { get; init; } = string.Empty;
}

/// <summary>
/// The fields the pipeline should come back with. A null is a real expectation, not a
/// blank: it asserts the parser leaves the field alone rather than inventing a value,
/// which is its stated contract.
/// </summary>
public sealed record OcrExpectation(
    string? Name,
    string? Roaster,
    string? Origin,
    string? RoastLevel,
    string? Weight);

/// <summary>
/// Loads the benchmark corpora and answers whether the engine can run here.
/// </summary>
public static class OcrFixtures
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The fixtures directory, copied next to the test assembly at build.</summary>
    public static string Root => System.IO.Path.Combine(AppContext.BaseDirectory, "Ocr", "Fixtures");

    /// <summary>
    /// Whether a benchmark run is possible: the CLI on PATH *and* the language data the
    /// adapter resolves. Both are asked through the adapter's own logic rather than
    /// re-derived, so a change to how tessdata is located cannot leave this lying.
    /// </summary>
    public static bool EngineAvailable =>
        ExecutableOnPath("tesseract")
        && new TesseractCliOcrService(
            Options.Create(new OcrOptions()),
            NullLogger<TesseractCliOcrService>.Instance).IsAvailable;

    /// <summary>
    /// Every fixture across both corpora, synthetic first. A corpus with no manifest is
    /// simply absent — `real/` ships empty, because a folder of photographs of my own
    /// shelf is not something to commit, and the benchmark has to run without it.
    /// </summary>
    public static IReadOnlyList<OcrFixture> All()
    {
        List<OcrFixture> fixtures = [];
        foreach (var corpus in (string[])["synthetic", "real"])
        {
            var directory = System.IO.Path.Combine(Root, corpus);
            var manifest = System.IO.Path.Combine(directory, "manifest.json");
            if (!System.IO.File.Exists(manifest))
            {
                continue;
            }

            var loaded = JsonSerializer.Deserialize<Manifest>(
                System.IO.File.ReadAllText(manifest), ManifestJson);

            fixtures.AddRange(
                (loaded?.Fixtures ?? []).Select(f => f with
                {
                    Path = System.IO.Path.Combine(directory, f.File),
                    Corpus = corpus,
                }));
        }

        return fixtures;
    }

    private static bool ExecutableOnPath(string name)
    {
        // Windows needs the extension; Unix does not. The adapter starts the process by
        // bare name, so this asks the same question the process start will.
        var candidates = OperatingSystem.IsWindows() ? new[] { $"{name}.exe", name } : [name];
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => candidates.Any(c =>
            {
                try
                {
                    return System.IO.File.Exists(System.IO.Path.Combine(dir, c));
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry is not a reason to fail discovery.
                    return false;
                }
            }));
    }

    private sealed record Manifest(IReadOnlyList<OcrFixture> Fixtures);
}

/// <summary>
/// A <see cref="FactAttribute"/> for the benchmark, skipped with a reason when the host
/// carries no Tesseract — the bare Windows host this repo is often driven from, per
/// CLAUDE.md § Gotchas. Skipped rather than quietly passing: a benchmark that reports
/// green having measured nothing is worse than one that says it did not run.
/// </summary>
public sealed class OcrBenchmarkFactAttribute : FactAttribute
{
    public OcrBenchmarkFactAttribute()
    {
        if (!OcrFixtures.EngineAvailable)
        {
            Skip = "tesseract (or its eng.traineddata) is not installed here; "
                + "run the benchmark in the dev container or in the ocr-bench CI job.";
        }
    }
}
