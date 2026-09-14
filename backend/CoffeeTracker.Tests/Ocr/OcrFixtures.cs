using System.Text.Json;
using System.Text.Json.Serialization;
using CoffeeTracker.Infrastructure.Ocr;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
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

    /// <summary>Which corpus it came from: <c>synthetic</c> or <c>real</c>.</summary>
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
    /// Which engine the benchmark scores. Defaults to the one the app ships with, so the
    /// number in CI is the number users get; set <c>OCR_BENCH_ENGINE=tesseract</c> to
    /// score the other one and compare them on the same images.
    /// </summary>
    public static string Engine =>
        Environment.GetEnvironmentVariable("OCR_BENCH_ENGINE") ?? new OcrOptions().Engine;

    /// <summary>
    /// The confidence gate the selected engine calls for, mirroring what
    /// <c>AddOcr</c> registers. Mirrored rather than shared because the benchmark builds
    /// its adapter by hand instead of resolving one out of the container.
    /// </summary>
    public static double Gate =>
        string.Equals(Engine, "tesseract", StringComparison.OrdinalIgnoreCase) ? 55 : 80;

    /// <summary>Builds the adapter named by <see cref="Engine"/>.</summary>
    public static IOcrService NewEngine(OcrOptions options, ILoggerFactory logs) =>
        string.Equals(Engine, "tesseract", StringComparison.OrdinalIgnoreCase)
            ? new TesseractCliOcrService(Options.Create(options), logs.CreateLogger<TesseractCliOcrService>())
            : new RapidOcrService(Options.Create(options), logs.CreateLogger<RapidOcrService>());

    /// <summary>
    /// Whether a benchmark run is possible here. Asked through the adapter's own
    /// availability check rather than re-derived, so a change to how either engine
    /// locates its files cannot leave this lying.
    /// </summary>
    public static bool EngineAvailable =>
        string.Equals(Engine, "tesseract", StringComparison.OrdinalIgnoreCase)
            ? ExecutableOnPath("tesseract")
                && new TesseractCliOcrService(
                    Options.Create(new OcrOptions()),
                    NullLogger<TesseractCliOcrService>.Instance).IsAvailable
            : ExecutableOnPath("python3")
                && new RapidOcrService(
                    Options.Create(new OcrOptions { RapidOcrScriptPath = RapidOcrScript }),
                    NullLogger<RapidOcrService>.Instance).IsAvailable;

    /// <summary>
    /// The reader script, found in the repo when running from a checkout and at its
    /// installed path in the container. A developer should not have to install the app
    /// to score it.
    /// </summary>
    public static string RapidOcrScript
    {
        get
        {
            var repo = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "deploy", "rapidocr", "read.py"));
            return System.IO.File.Exists(repo) ? repo : "/opt/rapidocr/read.py";
        }
    }

    /// <summary>
    /// Every fixture across both corpora, synthetic first. A corpus whose manifest is
    /// missing is skipped rather than failing the run, so a checkout carrying only one of
    /// the two still scores that one.
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
/// carries no Tesseract, which is the bare Windows host this repo is often driven from
/// (CLAUDE.md § Gotchas). Skipped rather than quietly passing: a benchmark that reports
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
