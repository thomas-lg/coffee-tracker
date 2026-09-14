using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Ocr;
using Xunit;

namespace CoffeeTracker.Tests;

// The piece that makes an engine changeable without a restart. What matters is that it
// asks the policy on every call rather than caching an answer, and that it reports the
// availability of the engine actually in force rather than of the pair.
public sealed class SwitchingOcrServiceTests
{
    private sealed class FakeEngine(string name, bool available = true) : IOcrService
    {
        public int Reads { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(available);

        public Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(OcrResult.Read(name));
        }
    }

    private sealed class FakePolicy(OcrEngine engine) : IOcrEnginePolicy
    {
        public OcrEngine Engine { get; set; } = engine;
        public int Reads { get; private set; }

        public Task<OcrEngine> GetAsync(CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(Engine);
        }

        public Task SetAsync(OcrEngine engine, CancellationToken ct = default)
        {
            Engine = engine;
            return Task.CompletedTask;
        }
    }

    private readonly FakeEngine _rapid = new("rapid");
    private readonly FakeEngine _tesseract = new("tesseract");
    private readonly FakeEngine _disabled = new("disabled", available: false);

    private SwitchingOcrService NewService(FakePolicy policy) =>
        new(policy, _rapid, _tesseract, _disabled);

    [Theory]
    [InlineData(OcrEngine.RapidOcr, "rapid")]
    [InlineData(OcrEngine.Tesseract, "tesseract")]
    [InlineData(OcrEngine.Disabled, "disabled")]
    public async Task A_scan_goes_to_the_engine_the_policy_names(OcrEngine chosen, string expected)
    {
        var read = await NewService(new FakePolicy(chosen)).ReadAsync(new MemoryStream());

        Assert.Equal(expected, read.RawText);
    }

    [Fact]
    public async Task Changing_the_engine_takes_effect_on_the_next_scan()
    {
        // The whole point of the feature: no restart, no cached resolution.
        var policy = new FakePolicy(OcrEngine.RapidOcr);
        var service = NewService(policy);

        await service.ReadAsync(new MemoryStream());
        policy.Engine = OcrEngine.Tesseract;
        await service.ReadAsync(new MemoryStream());

        Assert.Equal(1, _rapid.Reads);
        Assert.Equal(1, _tesseract.Reads);
    }

    [Fact]
    public async Task Availability_is_the_chosen_engine_and_not_merely_some_engine()
    {
        // The reason the port's check is asynchronous. Answering "either of them could
        // run" would let a scan through to an engine that cannot.
        var policy = new FakePolicy(OcrEngine.Disabled);

        Assert.False(await NewService(policy).IsAvailableAsync());

        policy.Engine = OcrEngine.RapidOcr;
        Assert.True(await NewService(policy).IsAvailableAsync());
    }

    [Fact]
    public async Task The_policy_is_consulted_per_call()
    {
        var policy = new FakePolicy(OcrEngine.RapidOcr);
        var service = NewService(policy);

        await service.IsAvailableAsync();
        await service.ReadAsync(new MemoryStream());

        Assert.Equal(2, policy.Reads);
    }
}
