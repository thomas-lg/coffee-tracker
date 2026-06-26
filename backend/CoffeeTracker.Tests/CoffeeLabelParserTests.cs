using CoffeeTracker.Application.Services;
using Xunit;

namespace CoffeeTracker.Tests;

// The parser is pure (no native deps), so it carries the automated coverage for
// the scan feature — CI has no Tesseract. Assertions are strict on the robust
// extractors (roast / weight / origin) and lenient on the fuzzy name/roaster.
public class CoffeeLabelParserTests
{
    private static readonly CoffeeLabelParser Parser = new();

    [Theory]
    [InlineData("Light Roast", "Light")]
    [InlineData("MEDIUM roast", "Medium")]
    [InlineData("Dark roast, espresso blend", "Dark")] // earliest in the text wins
    [InlineData("Espresso · dark", "Espresso")]        // ... and here Espresso is first
    [InlineData("Medium-Dark", "Medium-Dark")]         // longest match wins on a tie
    public void Parse_ExtractsRoastLevel(string text, string expected) =>
        Assert.Equal(expected, Parser.Parse(text).RoastLevel);

    [Theory]
    [InlineData("Net wt 250g", "250g")]
    [InlineData("1 kg bag", "1kg")]
    [InlineData("12 oz", "12oz")]
    [InlineData("340 GRAMS", "340g")]
    [InlineData("0,25 kg", "0.25kg")]
    public void Parse_ExtractsWeight(string text, string expected) =>
        Assert.Equal(expected, Parser.Parse(text).Weight);

    [Theory]
    [InlineData("Single origin: Ethiopia", "Ethiopia")]
    [InlineData("Grown in Colombia, Huila", "Colombia")] // Colombia ranks before Huila
    [InlineData("Costa Rica Tarrazu", "Costa Rica")]
    public void Parse_ExtractsOrigin(string text, string expected) =>
        Assert.Equal(expected, Parser.Parse(text).Origin);

    [Fact]
    public void Parse_ReturnsAllNull_OnUnrecognizedText()
    {
        var result = Parser.Parse("??? !!! 42 #$%");

        Assert.Null(result.Name);
        Assert.Null(result.Roaster);
        Assert.Null(result.Origin);
        Assert.Null(result.RoastLevel);
        Assert.Null(result.Weight);
    }

    [Fact]
    public void Parse_DoesNotThrow_OnEmptyOrNull()
    {
        Assert.NotNull(Parser.Parse(string.Empty));
        Assert.NotNull(Parser.Parse(null!));
    }

    [Fact]
    public void Parse_FindsNameAndRoaster_OnAMultiLineLabel()
    {
        var text = "Stumptown Coffee Roasters\nHair Bender\nMedium · Ethiopia\n340g";

        var result = Parser.Parse(text);

        Assert.NotNull(result.Name);
        // The roaster line is recognized by its "Coffee Roasters" keyword.
        Assert.Equal("Stumptown Coffee Roasters", result.Roaster);
        Assert.Equal("Medium", result.RoastLevel);
        Assert.Equal("Ethiopia", result.Origin);
        Assert.Equal("340g", result.Weight);
    }

    [Fact]
    public void Parse_PrefersOriginOnItsOwnLine_OverTastingNoteMention()
    {
        // A country mentioned in a tasting-note sentence must not beat the origin
        // printed on its own (label-like) line, even when the note comes first.
        var result = Parser.Parse("Tasting notes: brazil nut, cocoa, dark berries\nEthiopia");

        Assert.Equal("Ethiopia", result.Origin);
    }

    [Fact]
    public void Parse_PrefersRoastOnItsOwnLine_OverTastingNoteMention()
    {
        // "Dark" in "dark chocolate" must not win over the actual roast on its own line.
        var result = Parser.Parse("Dark chocolate & caramel notes\nLight Roast");

        Assert.Equal("Light", result.RoastLevel);
    }

    [Fact]
    public void Parse_PicksEarlierOrigin_WhenBothOnSameLine()
    {
        // No label-like line to disambiguate: fall back to first-mentioned.
        var result = Parser.Parse("Grown in Colombia, finished like Brazil");

        Assert.Equal("Colombia", result.Origin);
    }

    [Fact]
    public void Parse_PrefersLongerRoast_OnTie()
    {
        Assert.Equal("Medium-Dark", Parser.Parse("Roast: Medium-Dark").RoastLevel);
    }

    [Fact]
    public void Parse_KeepsNameLine_EvenWhenItCarriesAWeight()
    {
        // Net weight on the same line as the name must not drop the name.
        var result = Parser.Parse("Ethiopia Natural 250g\nSunrise Roastery");

        Assert.Equal("Ethiopia Natural 250g", result.Name);
        Assert.Equal("250g", result.Weight);
    }

    [Fact]
    public void Parse_DoesNotDuplicateNameAsRoaster()
    {
        // Single prominent line: it's the name; roaster stays null rather than echoing it.
        var result = Parser.Parse("Blue Bottle Coffee");

        Assert.Equal("Blue Bottle Coffee", result.Name);
        Assert.Null(result.Roaster);
    }

    [Fact]
    public void Parse_DoesNotThrow_OnDuplicatedRoasterLines()
    {
        // Two identical roaster-keyword lines must not throw (the roaster-first
        // branch's name pick falls back instead of First() blowing up). We assert
        // the never-throw contract + a sane roaster; we don't pin the Name, which
        // is a heuristic detail free to evolve.
        var result = Parser.Parse("Stumptown Roasters\nStumptown Roasters");

        Assert.Equal("Stumptown Roasters", result.Roaster);
    }

    [Fact]
    public void Parse_DoesNotInvertNameAndRoaster_WhenNameLineMentionsCoffee()
    {
        // Bare "Coffee" on the product line must NOT make it the roaster.
        var result = Parser.Parse("Ethiopia Coffee\nBlue Bottle Roasters");

        Assert.Equal("Ethiopia Coffee", result.Name);
        Assert.Equal("Blue Bottle Roasters", result.Roaster);
    }

    [Theory]
    [InlineData("Roast: Medium Dark", "Medium-Dark")]
    [InlineData("Light Medium roast", "Light-Medium")]
    public void Parse_NormalizesSpacedRoast(string text, string expected) =>
        Assert.Equal(expected, Parser.Parse(text).RoastLevel);

    [Fact]
    public void Parse_DoesNotPromoteWeightLineToRoaster()
    {
        var result = Parser.Parse("Hair Bender\nNet wt 340 grams");

        Assert.Equal("Hair Bender", result.Name);
        Assert.Null(result.Roaster);
    }
}
