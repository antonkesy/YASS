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
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pdf);
        // Black and white is lossless by nature, and as a 1-bit PNG NAPS2 writes it as CCITT G4:
        // a fraction of the size of the JPEG that would otherwise carry it.
        ExportFormat format = pdf.Compression == PdfCompression.Lossless || page.ColorMode == ColorMode.Bw ? ExportFormat.Png : ExportFormat.Jpg;
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + (format == ExportFormat.Png ? ".png" : ".jpg"));
        FileStream file = File.Create(path);
        await using (file.ConfigureAwait(false))
        {
            await RenderAsync(page, Encoder(format, page.ColorMode, pdf.JpegQuality, forPdf: true), file, cancellationToken).ConfigureAwait(false);
        }

        return path;
    }

    /// <summary>
    /// Renders a page straight to a stream in the requested image format. This is the JPG and
    /// PNG export path, where each page becomes a file of its own rather than a PDF page.
    /// </summary>
    public static Task RenderToStreamAsync(ExportPage page, ExportFormat format, PdfConfig quality, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(quality);
        return RenderAsync(page, Encoder(format, page.ColorMode, quality.JpegQuality, forPdf: false), destination, cancellationToken);
    }

    private static async Task RenderAsync(ExportPage page, IImageEncoder encoder, Stream destination, CancellationToken cancellationToken)
    {
        using Image image = await Image.LoadAsync(page.SourcePath, cancellationToken).ConfigureAwait(false);
        PagePipeline.Apply(image, page.Spec);

        // ImageSharp reports 96 dpi for files without resolution metadata, so "absent" is indistinguishable
        // from "96"; the scan resolution is authoritative and is stamped unconditionally.
        ImageMetadata meta = image.Metadata;
        meta.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        meta.HorizontalResolution = page.Dpi;
        meta.VerticalResolution = page.Dpi;

        await image.SaveAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The encoder for a page in <paramref name="mode"/>. A PNG keeps only the channels the page has
    /// (the pipeline leaves a grey page in an RGB image). A grey JPEG is luminance-only on its own,
    /// but colour-encoded for a PDF: NAPS2 embeds that verbatim, where it would decode and re-encode
    /// a luminance-only one.
    /// </summary>
    private static IImageEncoder Encoder(ExportFormat format, ColorMode mode, int jpegQuality, bool forPdf) =>
        format == ExportFormat.Png
            ? mode switch
            {
                ColorMode.Bw => new PngEncoder { ColorType = PngColorType.Grayscale, BitDepth = PngBitDepth.Bit1 },
                ColorMode.Grayscale => new PngEncoder { ColorType = PngColorType.Grayscale },
                _ => new PngEncoder(),
            }
            : new JpegEncoder
            {
                Quality = jpegQuality,
                ColorType = mode == ColorMode.Color || forPdf ? JpegEncodingColor.YCbCrRatio420 : JpegEncodingColor.Luminance,
            };
}
