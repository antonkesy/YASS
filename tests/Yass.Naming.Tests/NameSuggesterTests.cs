using Yass.Core.Naming;

namespace Yass.Naming.Tests;

public class HeadingTests
{
    private static PageLine Line(string text, int size, int top) => new(text, size, top);

    [Fact]
    public void Picks_the_largest_line_near_the_top()
    {
        List<PageLine> page =
        [
            Line("ACME Industries", 10, 20),
            Line("Invoice for September", 22, 90),
            Line("Item description goes here at some length", 10, 400),
            Line("Another body line of roughly the same size", 10, 900),
        ];

        NameSuggestion suggestion = Assert.IsType<NameSuggestion>(NameSuggester.Heading(page));

        Assert.Equal("Invoice For September", suggestion.Name);
        Assert.Equal(NameSource.Ocr, suggestion.Source);
        Assert.True(suggestion.Confidence > 0.6, $"standing out should raise confidence, got {suggestion.Confidence}");
    }

    [Fact]
    public void Is_less_confident_when_nothing_stands_out()
    {
        List<PageLine> page =
        [
            Line("Some plain first line here", 10, 10),
            Line("Another plain line of text", 10, 400),
            Line("And a third plain line here", 10, 900),
        ];

        NameSuggestion suggestion = Assert.IsType<NameSuggestion>(NameSuggester.Heading(page));

        Assert.True(suggestion.Confidence < 0.6, $"uniform text should not look confident, got {suggestion.Confidence}");
    }

    [Fact]
    public void Ignores_page_furniture()
    {
        List<PageLine> page =
        [
            Line("1", 30, 10),
            Line("2026-09-24", 28, 20),
            Line("...", 40, 30),
            Line("Quarterly Report", 14, 60),
            Line("body text line for the median", 10, 800),
        ];

        Assert.Equal("Quarterly Report", NameSuggester.Heading(page)!.Name);
    }

    [Fact]
    public void Gives_up_when_the_top_of_the_page_has_nothing_usable()
    {
        List<PageLine> page = [Line("7", 30, 5), Line("body text down here", 10, 900)];

        Assert.Null(NameSuggester.Heading(page));
    }
}

public class AiNameCleaningTests
{
    [Theory]
    [InlineData("\"Tax Return 2025.pdf\"", "Tax Return 2025")]
    [InlineData("invoice acme september", "Invoice Acme September")]
    [InlineData("Report.PDF\nplus an explanation", "Report")]
    [InlineData("  bank/statement:march  ", "Bankstatementmarch")]
    public void Strips_quotes_extensions_and_extra_lines(string raw, string expected)
    {
        Assert.Equal(expected, AiNamer.Clean(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(null)]
    public void Rejects_what_is_not_a_name(string? raw)
    {
        Assert.Null(AiNamer.Clean(raw));
    }
}
