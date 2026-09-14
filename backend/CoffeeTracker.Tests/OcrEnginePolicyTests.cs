using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Ocr;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// The engine moved from configuration to a stored setting, and the risky part of that
// move is what happens to an instance that never chooses: it must keep scanning exactly
// as its deployment said, or an upgrade silently changes how every scan reads.
public sealed class OcrEnginePolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OcrEnginePolicyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private EfOcrEnginePolicy NewPolicy(string configured = "rapidocr") =>
        new(new AppDbContext(_options), Options.Create(new OcrOptions { Engine = configured }));

    [Theory]
    [InlineData("rapidocr", OcrEngine.RapidOcr)]
    [InlineData("tesseract", OcrEngine.Tesseract)]
    [InlineData("none", OcrEngine.Disabled)]
    public async Task An_instance_nobody_has_configured_scans_the_way_its_deployment_says(
        string configured, OcrEngine expected)
    {
        Assert.Equal(expected, await NewPolicy(configured).GetAsync());
    }

    [Fact]
    public async Task A_chosen_engine_outranks_the_configured_one()
    {
        await NewPolicy(configured: "rapidocr").SetAsync(OcrEngine.Tesseract);

        Assert.Equal(OcrEngine.Tesseract, await NewPolicy(configured: "rapidocr").GetAsync());
    }

    [Fact]
    public async Task Choosing_twice_keeps_the_second_choice()
    {
        await NewPolicy().SetAsync(OcrEngine.Tesseract);
        await NewPolicy().SetAsync(OcrEngine.Disabled);

        Assert.Equal(OcrEngine.Disabled, await NewPolicy().GetAsync());
    }

    [Fact]
    public async Task A_value_nobody_recognises_falls_back_rather_than_switching_scanning_off()
    {
        // Only a hand-edited database gets here. Disabling the feature would be the worst
        // possible way to report a typo, so the configured default wins instead.
        await using (var db = new AppDbContext(_options))
        {
            db.AppSettings.Add(new AppSettings { Id = AppSettings.SingletonId, OcrEngine = "paddle" });
            await db.SaveChangesAsync();
        }

        Assert.Equal(OcrEngine.Tesseract, await NewPolicy(configured: "tesseract").GetAsync());
    }

    [Fact]
    public async Task The_choice_is_stored_by_name_so_the_row_stays_readable()
    {
        await NewPolicy().SetAsync(OcrEngine.Tesseract);

        await using var db = new AppDbContext(_options);
        var row = await db.AppSettings.AsNoTracking().FirstAsync();
        // Not an ordinal: reordering the enum would otherwise repoint every instance at
        // a different engine on the next deploy.
        Assert.Equal("Tesseract", row.OcrEngine);
    }

    [Fact]
    public async Task Choosing_an_engine_leaves_the_account_policy_alone()
    {
        // Both live in the same singleton row, so a careless write here could reopen
        // registration on an instance that had deliberately closed it.
        await using (var db = new AppDbContext(_options))
        {
            db.AppSettings.Add(new AppSettings
            {
                Id = AppSettings.SingletonId,
                LocalLoginEnabled = true,
                LocalRegistrationEnabled = false,
            });
            await db.SaveChangesAsync();
        }

        await NewPolicy().SetAsync(OcrEngine.Tesseract);

        await using var after = new AppDbContext(_options);
        var row = await after.AppSettings.AsNoTracking().FirstAsync();
        Assert.True(row.LocalLoginEnabled);
        Assert.False(row.LocalRegistrationEnabled);
    }
}
