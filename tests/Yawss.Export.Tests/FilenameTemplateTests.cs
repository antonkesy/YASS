namespace Yawss.Export.Tests;

public class FilenameTemplateTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 9, 24, 14, 5, 9, TimeSpan.Zero);

    [Fact]
    public void Default_template_expands_date_and_time()
    {
        string name = FilenameTemplate.Expand("{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf", Stamp);

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_\d{4}_scan\.pdf$", name);
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
