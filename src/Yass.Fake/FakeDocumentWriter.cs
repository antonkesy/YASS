using System.Globalization;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Fake;

/// <summary>
/// Writes a real PDF of the scanned pages without NAPS2: each page is decoded, rotated and
/// greyed the way the edits ask, encoded as JPEG and embedded whole. What comes out opens in
/// any reader and shows the document, which is the point — a placeholder that only claimed to
/// be a page proved nothing about the save flow and made the demo look broken.
/// </summary>
public sealed class FakeDocumentWriter : IDocumentWriter
{
    public async Task WritePdfAsync(
        IReadOnlyList<ExportPage> pages,
        PdfConfig pdf,
        DocumentMetadata metadata,
        Stream destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(destination);

        var rendered = new List<Rendered>(pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            rendered.Add(await RenderAsync(pages[i], pdf.JpegQuality, cancellationToken).ConfigureAwait(false));
            progress?.Report((i + 1) * 90 / Math.Max(pages.Count, 1));
        }

        byte[] bytes = Build(pages, rendered, metadata);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        progress?.Report(100);
    }

    /// <summary>The page as its own file, encoded for real so an image export is a real image.</summary>
    public async Task WriteImageAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(quality);
        ArgumentNullException.ThrowIfNull(destination);

        using Image<Rgb24> image = await LoadAsync(page, cancellationToken).ConfigureAwait(false);
        if (format == ExportFormat.Png)
        {
            await image.SaveAsPngAsync(destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        await image.SaveAsJpegAsync(destination, Encoder(page, quality.JpegQuality), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What a page went through, for the PDF's title. Handy when a save looks wrong.</summary>
    public static string PageLabel(ExportPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return $"{Path.GetFileName(page.SourcePath)} rot={(int)page.Rotation} color={page.ColorMode}";
    }

    /// <summary>One page's JPEG and the size it should occupy, in points.</summary>
    private sealed record Rendered(byte[] Jpeg, int Width, int Height, bool Gray, double Points, double PointsHigh);

    private static async Task<Image<Rgb24>> LoadAsync(ExportPage page, CancellationToken cancellationToken)
    {
        Image<Rgb24> image = await Image.LoadAsync<Rgb24>(page.SourcePath, cancellationToken).ConfigureAwait(false);
        RotateMode rotate = page.Rotation switch
        {
            Rotation.R90 => RotateMode.Rotate90,
            Rotation.R180 => RotateMode.Rotate180,
            Rotation.R270 => RotateMode.Rotate270,
            _ => RotateMode.None,
        };

        image.Mutate(x =>
        {
            if (rotate != RotateMode.None)
            {
                x.Rotate(rotate);
            }

            if (page.ColorMode != ColorMode.Color)
            {
                x.Grayscale();
            }
        });

        return image;
    }

    private static JpegEncoder Encoder(ExportPage page, int quality) => new()
    {
        Quality = Math.Clamp(quality, 1, 100),
        ColorType = page.ColorMode == ColorMode.Color ? JpegEncodingColor.YCbCrRatio420 : JpegEncodingColor.Luminance,
    };

    private static async Task<Rendered> RenderAsync(ExportPage page, int quality, CancellationToken cancellationToken)
    {
        using Image<Rgb24> image = await LoadAsync(page, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await image.SaveAsJpegAsync(buffer, Encoder(page, quality), cancellationToken).ConfigureAwait(false);

        // 72 points to the inch: the page is however large the pixels are at the dpi they were
        // scanned at, which is what makes an A4 page come back out as A4.
        double dpi = page.Dpi > 0 ? page.Dpi : 300;
        return new Rendered(
            buffer.ToArray(),
            image.Width,
            image.Height,
            page.ColorMode != ColorMode.Color,
            Math.Round(image.Width * 72.0 / dpi, 2),
            Math.Round(image.Height * 72.0 / dpi, 2));
    }

    private static byte[] Build(IReadOnlyList<ExportPage> pages, IReadOnlyList<Rendered> rendered, DocumentMetadata metadata)
    {
        // Object layout: 1 catalog, 2 pages, 3 info, then per page: page, content, image.
        var body = new List<byte[]>();
        int first = 4;
        string kids = string.Join(" ", Enumerable.Range(0, rendered.Count).Select(i => $"{first + i * 3} 0 R"));
        // The same fields the real writer stamps, so a demo PDF's properties read the same.
        string title = metadata.Title ?? (pages.Count > 0 ? PageLabel(pages[0]) : "YASS fake scan");
        var info = new StringBuilder($"<< /Title ({Escape(title)}) /Producer (YASS fake writer) /Creator (YASS)");
        if (!string.IsNullOrWhiteSpace(metadata.Author))
        {
            info.Append($" /Author ({Escape(metadata.Author.Trim())})");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Subject))
        {
            info.Append($" /Subject ({Escape(metadata.Subject.Trim())})");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Keywords))
        {
            info.Append($" /Keywords ({Escape(metadata.Keywords.Trim())})");
        }

        info.Append(" >>");

        body.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        body.Add(Ascii($"<< /Type /Pages /Kids [{kids}] /Count {rendered.Count} >>"));
        body.Add(Ascii(info.ToString()));

        for (int i = 0; i < rendered.Count; i++)
        {
            Rendered page = rendered[i];
            int pageObj = first + i * 3;
            int contentObj = pageObj + 1;
            int imageObj = pageObj + 2;

            string content = string.Create(
                CultureInfo.InvariantCulture,
                $"q {page.Points} 0 0 {page.PointsHigh} 0 0 cm /Im0 Do Q");

            body.Add(Ascii(string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page.Points} {page.PointsHigh}] " +
                $"/Resources << /XObject << /Im0 {imageObj} 0 R >> >> /Contents {contentObj} 0 R >>")));
            body.Add(Ascii($"<< /Length {content.Length} >>\nstream\n{content}\nendstream"));
            body.Add(Stream(page));
        }

        return Assemble(body);
    }

    /// <summary>An image XObject: the JPEG goes in whole, which is what /DCTDecode means.</summary>
    private static byte[] Stream(Rendered page)
    {
        string header = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /XObject /Subtype /Image /Width {page.Width} /Height {page.Height} " +
            $"/ColorSpace /{(page.Gray ? "DeviceGray" : "DeviceRGB")} /BitsPerComponent 8 " +
            $"/Filter /DCTDecode /Length {page.Jpeg.Length} >>\nstream\n");

        var bytes = new List<byte>(header.Length + page.Jpeg.Length + 16);
        bytes.AddRange(Encoding.ASCII.GetBytes(header));
        bytes.AddRange(page.Jpeg);
        bytes.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
        return [.. bytes];
    }

    /// <summary>Objects, cross-reference table and trailer, counted in bytes because of the JPEGs.</summary>
    private static byte[] Assemble(List<byte[]> body)
    {
        using var output = new MemoryStream();
        void Write(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n");
        var offsets = new List<long>(body.Count);
        for (int i = 0; i < body.Count; i++)
        {
            offsets.Add(output.Length);
            Write($"{i + 1} 0 obj\n");
            output.Write(body[i], 0, body[i].Length);
            Write("\nendobj\n");
        }

        long xref = output.Length;
        Write($"xref\n0 {body.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets)
        {
            Write(string.Create(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n"));
        }

        Write(string.Create(
            CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {body.Count + 1} /Root 1 0 R /Info 3 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return output.ToArray();
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
