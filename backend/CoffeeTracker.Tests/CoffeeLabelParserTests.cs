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
    public void Parse_PicksOriginByTextPosition_NotArrayOrder()
    {
        // Brazil appears first in the text but Ethiopia is earlier in the keyword
        // array; the actual (first-mentioned) origin should win.
        var result = Parser.Parse("Tasting notes of Brazil nut; blended with Ethiopia naturals");

        Assert.Equal("Brazil", result.Origin);
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
        // Single prominent line that also matches the roaster keyword: it's the
        // name; roaster stays null rather than echoing it.
        var result = Parser.Parse("Blue Bottle Coffee");

        Assert.Equal("Blue Bottle Coffee", result.Name);
        Assert.Null(result.Roaster);
    }
}
