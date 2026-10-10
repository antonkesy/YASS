using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;

namespace Yass.Export;

/// <summary>
/// Bakes a page's edits into a single encoded file NAPS2 can import and embed as-is: JPEG at the configured
/// quality, or PNG for lossless. DPI metadata is preserved (or set from the scan options) so the PDF page
/// gets its physical size right. See ADR-004 for why this happens outside NAPS2.
/// </summary>
public static class PageRenderer
{
    /// <summary>Renders a page into a new file in <paramref name="directory"/> for the PDF, and returns its path.</summary>
    public static async Task<string> RenderToFileAsync(ExportPage page, PdfConfig pdf, string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ExportFormat format = pdf.Compression == PdfCompression.Lossless ? ExportFormat.Png : ExportFormat.Jpg;
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + (format == ExportFormat.Png ? ".png" : ".jpg"));
        FileStream file = File.Create(path);
        await using (file.ConfigureAwait(false))
        {
            await RenderToStreamAsync(page, format, pdf, file, cancellationToken).ConfigureAwait(false);
        }

        return path;
    }

    /// <summary>
    /// Renders a page straight to a stream in the requested image format. This is the JPG and
    /// PNG export path, where each page becomes a file of its own rather than a PDF page.
    /// </summary>
    public static async Task RenderToStreamAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(quality);

        using Image image = await Image.LoadAsync(page.SourcePath, cancellationToken).ConfigureAwait(false);
        PagePipeline.Apply(image, page.Spec);

        // ImageSharp reports 96 dpi for files without resolution metadata, so "absent" is indistinguishable
        // from "96"; the scan resolution is authoritative and is stamped unconditionally.
        ImageMetadata meta = image.Metadata;
        meta.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        meta.HorizontalResolution = page.Dpi;
        meta.VerticalResolution = page.Dpi;

        IImageEncoder encoder = format == ExportFormat.Png
            ? new PngEncoder()
            : new JpegEncoder { Quality = quality.JpegQuality, ColorType = page.ColorMode == ColorMode.Color ? JpegEncodingColor.YCbCrRatio420 : JpegEncodingColor.Luminance };
        await image.SaveAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
    }
}
