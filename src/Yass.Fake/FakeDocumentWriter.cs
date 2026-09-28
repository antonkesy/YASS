using System.Globalization;
using System.Text;
using Yass.Core.Config;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Fake;

/// <summary>
/// Writes a small but valid PDF with one text-only page per <see cref="ExportPage"/>, so the save flow can be
/// exercised end to end without NAPS2. The page text records the source file and the edits that would apply.
/// </summary>
public sealed class FakeDocumentWriter : IDocumentWriter
{
    public async Task WritePdfAsync(
        IReadOnlyList<ExportPage> pages,
        PdfConfig pdf,
        Stream destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(destination);

        byte[] bytes = Build(pages);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        progress?.Report(100);
    }

    /// <summary>
    /// Stands in for an encoded image: the same one-line label the fake PDF uses, so an image
    /// export can be asserted on without decoding anything.
    /// </summary>
    public async Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(destination);
        byte[] bytes = Encoding.ASCII.GetBytes($"FAKE-{format.ToString().ToUpperInvariant()} {PageLabel(page)}\n");
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Text lines of the fake PDF, one per page, in output order. Used by tests.</summary>
    public static string PageLabel(ExportPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return $"{Path.GetFileName(page.SourcePath)} rot={(int)page.Rotation} color={page.ColorMode}";
    }

    private static byte[] Build(IReadOnlyList<ExportPage> pages)
    {
        // Object layout: 1 catalog, 2 pages, 3 font, then per page: page object + content stream.
        var objects = new List<string>();
        int pageCount = Math.Max(pages.Count, 0);
        int firstPageObj = 4;
        string kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{firstPageObj + i * 2} 0 R"));

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (int i = 0; i < pageCount; i++)
        {
            int pageObj = firstPageObj + i * 2;
            int contentObj = pageObj + 1;
            string text = Escape($"YASS fake page {i + 1}: {PageLabel(pages[i])}");
            string content = $"BT /F1 14 Tf 40 800 Td ({text}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObj} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        int xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
