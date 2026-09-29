using Microsoft.Extensions.Logging.Abstractions;
using Yass.Core.Config;
using Yass.Core.Naming;

namespace Yass.Naming.Tests;

/// <summary>
/// Exercises the real OCR path against a page that looks like a document: a large heading,
/// a smaller subtitle and several lines of prose. The fake scanner's pages are deliberately
/// sparse and Tesseract finds nothing in them, so they cannot stand in for this.
/// </summary>
public sealed class OcrPipelineTests : IDisposable
{
    private static readonly string Page = Path.Combine(AppContext.BaseDirectory, "Fixtures", "quarterly-report.png");

    private readonly TesseractTextReader _reader = new(NullLogger<TesseractTextReader>.Instance);
    private readonly HttpClient _http = new();

    private static NamingConfig Config => new() { Ocr = true, OcrLanguage = "eng", OcrPages = 1, TimeoutSeconds = 30 };

    [Fact]
    public async Task Reads_the_lines_of_a_page()
    {
        if (!_reader.Available)
        {
            // Without the binary the only correct behaviour is to read nothing, quietly.
            Assert.Empty(await _reader.ReadLinesAsync(Page, Config, CancellationToken.None));
            return;
        }

        IReadOnlyList<PageLine> lines = await _reader.ReadLinesAsync(Page, Config, CancellationToken.None);

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Text.Contains("Quarterly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Text.Contains("Northwind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Suggests_the_heading_and_the_template_without_a_model()
    {
        var suggester = new NameSuggester(
            _reader,
            new AiNamer(_http, NullLogger<AiNamer>.Instance),
            () => Config,
            NullLogger<NameSuggester>.Instance);

        NameSuggestionSet found =
            await suggester.SuggestAsync([Page], "2026-09-24_scan", CancellationToken.None);

        IReadOnlyList<NameSuggestion> suggestions = found.Suggestions;
        Assert.Null(found.Error);

        if (!_reader.Available)
        {
            // No reader: the template is all there is, and it is still offered.
            Assert.Equal(NameSource.Template, Assert.Single(suggestions).Source);
            Assert.Null(found.Text);
            return;
        }

        // The heading outranks the template, and no model was configured.
        NameSuggestion best = suggestions[0];
        Assert.Equal(NameSource.Ocr, best.Source);
        Assert.Contains("Quarterly Report", best.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(best.Confidence > 0.6, $"a heading twice the body size should be confident, got {best.Confidence}");
        Assert.Contains(suggestions, s => s.Source == NameSource.Template);
        Assert.DoesNotContain(suggestions, s => s.Source == NameSource.Ai);

        // The text comes back whole, so a browser can ask its own model the same question.
        Assert.Contains("Quarterly", found.Text ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _reader.Dispose();
        _http.Dispose();
    }
}
