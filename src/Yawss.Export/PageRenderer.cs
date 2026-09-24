using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Processing;
using Yawss.Core.Config;
using Yawss.Core.Domain;
using Yawss.Core.Export;

namespace Yawss.Export;

/// <summary>
/// Bakes a page's edits into a single encoded file NAPS2 can import and embed as-is: JPEG at the configured
/// quality, or PNG for lossless. DPI metadata is preserved (or set from the scan options) so the PDF page
/// gets its physical size right. See ADR-004 for why this happens outside NAPS2.
/// </summary>
public static class PageRenderer
{
    public static async Task<string> RenderToFileAsync(ExportPage page, PdfConfig pdf, string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pdf);

        bool lossless = pdf.Compression == PdfCompression.Lossless;
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + (lossless ? ".png" : ".jpg"));

        using Image image = await Image.LoadAsync(page.SourcePath, cancellationToken).ConfigureAwait(false);
        image.Mutate(ctx =>
        {
            if (page.Rotation != Rotation.R0)
            {
                ctx.Rotate((float)(int)page.Rotation);
            }

            switch (page.ColorMode)
            {
                case ColorMode.Grayscale:
                    ctx.Grayscale();
                    break;
                case ColorMode.Bw:
                    ctx.Grayscale().BinaryThreshold(0.55f);
                    break;
                default:
                    break;
            }
        });

        // ImageSharp reports 96 dpi for files without resolution metadata, so "absent" is indistinguishable
        // from "96"; the scan resolution is authoritative and is stamped unconditionally.
        ImageMetadata meta = image.Metadata;
        meta.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        meta.HorizontalResolution = page.Dpi;
        meta.VerticalResolution = page.Dpi;

        IImageEncoder encoder = lossless
            ? new PngEncoder { CompressionLevel = PngCompressionLevel.DefaultCompression }
            : new JpegEncoder { Quality = pdf.JpegQuality, ColorType = page.ColorMode == ColorMode.Color ? JpegEncodingColor.YCbCrRatio420 : JpegEncodingColor.Luminance };
        await image.SaveAsync(path, encoder, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
