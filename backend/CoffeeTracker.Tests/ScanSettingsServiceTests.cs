using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Services;
using Xunit;

namespace CoffeeTracker.Tests;

// The administrator's view of scanning. Unlike the account policy there is no refusal
// path here, so what has to hold is that the answer always carries the availability of
// every option: that is what stops the screen offering an engine this host cannot run.
public sealed class ScanSettingsServiceTests
{
    private sealed class FakePolicy(OcrEngine engine) : IOcrEnginePolicy
    {
        public OcrEngine Engine { get; private set; } = engine;

        public Task<OcrEngine> GetAsync(CancellationToken ct = default) => Task.FromResult(Engine);

        public Task SetAsync(OcrEngine engine, CancellationToken ct = default)
        {
            Engine = engine;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCatalogue(bool rapid, bool tesseract) : IOcrEngineCatalogue
    {
        public Task<IReadOnlyList<ScanEngineOptionDto>> OptionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ScanEngineOptionDto>>(
            [
                new(OcrEngine.RapidOcr, rapid),
                new(OcrEngine.Tesseract, tesseract),
                new(OcrEngine.Disabled, true),
            ]);
    }

    private static ScanSettingsService NewService(
        FakePolicy policy, bool rapid = true, bool tesseract = true) =>
        new(policy, new FakeCatalogue(rapid, tesseract));

    [Fact]
    public async Task Reading_reports_the_engine_in_force_with_every_option()
    {
        var settings = await NewService(new FakePolicy(OcrEngine.Tesseract)).GetAsync();

        Assert.Equal(OcrEngine.Tesseract, settings.Engine);
        Assert.Equal(3, settings.Options.Count);
    }

    [Fact]
    public async Task An_engine_this_host_cannot_run_is_reported_as_unavailable()
    {
        // The screen disables the option on this, so an administrator cannot choose an
        // engine that would answer 503 to every scan.
        var settings = await NewService(new FakePolicy(OcrEngine.RapidOcr), tesseract: false).GetAsync();

        Assert.False(settings.Options.Single(o => o.Engine == OcrEngine.Tesseract).Available);
        Assert.True(settings.Options.Single(o => o.Engine == OcrEngine.RapidOcr).Available);
    }

    [Fact]
    public async Task Turning_scanning_off_is_always_on_offer()
    {
        // It needs nothing installed, so a host carrying no engine at all still has a
        // valid choice to make rather than a screen of dead options.
        var settings = await NewService(new FakePolicy(OcrEngine.Disabled), rapid: false, tesseract: false)
            .GetAsync();

        Assert.True(settings.Options.Single(o => o.Engine == OcrEngine.Disabled).Available);
    }

    [Fact]
    public async Task Updating_stores_the_choice_and_answers_with_what_is_now_in_force()
    {
        var policy = new FakePolicy(OcrEngine.RapidOcr);

        var settings = await NewService(policy).UpdateAsync(OcrEngine.Tesseract);

        Assert.Equal(OcrEngine.Tesseract, policy.Engine);
        // Answered from the store rather than echoed, so a client re-renders from truth.
        Assert.Equal(OcrEngine.Tesseract, settings.Engine);
    }
}
