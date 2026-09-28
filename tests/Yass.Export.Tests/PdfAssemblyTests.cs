using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Fake;

namespace Yass.Export.Tests;

public sealed class PdfAssemblyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yass-export-tests", Guid.NewGuid().ToString("N"));
    private readonly IReadOnlyList<string> _fixtures;
    private readonly Naps2PdfWriter _writer = new(NullLogger<Naps2PdfWriter>.Instance);

    public PdfAssemblyTests() => _fixtures = Fixtures.ExtractAll(_dir);

    public void Dispose()
    {
        _writer.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private static int PageCount(string pdf) => Regex.Matches(pdf, @"/Type\s*/Page\b").Count;

    private static (double W, double H)[] MediaBoxes(string pdf) =>
        Regex.Matches(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]")
            .Select(m => (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

    private async Task<string> WriteAsync(IReadOnlyList<ExportPage> pages, PdfConfig? pdf = null, IProgress<int>? progress = null)
    {
        using var ms = new MemoryStream();
        await _writer.WritePdfAsync(pages, pdf ?? new PdfConfig(), ms, progress, CancellationToken.None);
        return Encoding.Latin1.GetString(ms.ToArray());
    }

    [Fact]
    public async Task Pdf_has_one_page_per_export_page_in_order()
    {
        string pdf = await WriteAsync([
            new ExportPage(_fixtures[0], Rotation.R0, ColorMode.Grayscale, 75),
            new ExportPage(_fixtures[1], Rotation.R0, ColorMode.Grayscale, 75),
            new ExportPage(_fixtures[3], Rotation.R0, ColorMode.Color, 75)]);

        Assert.StartsWith("%PDF-", pdf, StringComparison.Ordinal);
        Assert.Equal(3, PageCount(pdf));
    }

    [Fact]
    public async Task Rotated_page_is_landscape_and_dpi_sets_the_page_size()
    {
        // Fixtures are 620x877 px; at 75 dpi that is A4 (595x842 pt).
        string pdf = await WriteAsync([
            new ExportPage(_fixtures[0], Rotation.R0, ColorMode.Grayscale, 75),
            new ExportPage(_fixtures[0], Rotation.R90, ColorMode.Grayscale, 75),
            new ExportPage(_fixtures[0], Rotation.R180, ColorMode.Grayscale, 150)]);

        (double W, double H)[] boxes = MediaBoxes(pdf);
        Assert.Equal(3, boxes.Length);
        Assert.InRange(boxes[0].W, 590, 600);
        Assert.InRange(boxes[0].H, 835, 848);
        Assert.InRange(boxes[1].W, 835, 848);
        Assert.InRange(boxes[1].H, 590, 600);
        Assert.InRange(boxes[2].W, 295, 300);
    }

    [Fact]
    public async Task Jpeg_compression_embeds_dct_and_lossless_does_not()
    {
        ExportPage[] pages = [new ExportPage(_fixtures[3], Rotation.R0, ColorMode.Color, 75)];

        string jpeg = await WriteAsync(pages, new PdfConfig { Compression = PdfCompression.Jpeg, JpegQuality = 50 });
        string lossless = await WriteAsync(pages, new PdfConfig { Compression = PdfCompression.Lossless });

        Assert.Contains("/DCTDecode", jpeg, StringComparison.Ordinal);
        Assert.DoesNotContain("/DCTDecode", lossless, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lower_jpeg_quality_yields_smaller_file()
    {
        ExportPage[] pages = [new ExportPage(_fixtures[3], Rotation.R0, ColorMode.Color, 75)];

        string hi = await WriteAsync(pages, new PdfConfig { JpegQuality = 95 });
        string lo = await WriteAsync(pages, new PdfConfig { JpegQuality = 30 });

        Assert.True(lo.Length < hi.Length);
    }

    [Fact]
    public async Task Progress_ends_at_100_and_grows_per_page()
    {
        var progress = new List<int>();
        var handler = new SynchronousProgress(progress.Add);

        await WriteAsync([
            new ExportPage(_fixtures[0], Rotation.R0, ColorMode.Grayscale, 75),
            new ExportPage(_fixtures[1], Rotation.R0, ColorMode.Grayscale, 75)], progress: handler);

        Assert.Equal([45, 90, 100], progress);
    }

    [Fact]
    public async Task Empty_page_list_is_rejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync([]));

    private sealed class SynchronousProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
