namespace Yass.Export.Tests;

public class FilenameTemplateTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 9, 24, 14, 5, 9, TimeSpan.Zero);

    [Fact]
    public void Default_template_expands_date_and_time()
    {
        string name = FilenameTemplate.Expand("{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf", Stamp);

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_\d{4}_scan\.pdf$", name);
    }

    /// <summary>
    /// <c>{NAME}</c> is the browser's, not ours: it stands for what Find name came back with,
    /// which may have been written by a model running in that browser. The browser fills it in
    /// before anything is sent, and this side must keep not knowing it — a well-meaning
    /// addition here would expand a token the browser had already resolved.
    /// </summary>
    [Fact]
    public void The_found_name_token_is_the_browsers_alone()
    {
        Assert.DoesNotContain("NAME", FilenameTemplate.Tokens, StringComparer.Ordinal);
        Assert.Contains("NAME", FilenameTemplate.UnknownTokens("{YEAR}_{NAME}"), StringComparer.Ordinal);
        Assert.Equal("2026_{NAME}.pdf", FilenameTemplate.Expand("{YEAR}_{NAME}", Stamp));
    }

    [Fact]
    public void Placeholders_without_format_use_sensible_defaults()
    {
        string name = FilenameTemplate.Expand("{date} {time} {session}", Stamp, "abc");

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{4} abc\.pdf$", name);
    }

    [Fact]
    public void Unsafe_characters_are_replaced_and_pdf_extension_enforced()
    {
        Assert.Equal("a_b_c.pdf", FilenameTemplate.Expand("a/b:c", Stamp));
        Assert.Equal("scan.pdf", FilenameTemplate.Expand("{session}", Stamp, ""));
        Assert.Equal("x.PDF", FilenameTemplate.Expand("x.PDF", Stamp));
    }
}
