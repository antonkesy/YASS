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

/// <summary>Bakes edits into one file NAPS2 can embed as-is, with DPI set for the PDF page size. Outside NAPS2 per SPEC.md §6.</summary>
public static class PageRenderer
{
    public static async Task<string> RenderToFileAsync(ExportPage page, PdfConfig pdf, string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pdf);
        // B&W as a 1-bit PNG is written by NAPS2 as CCITT G4, far smaller than a JPEG.
        ExportFormat format = pdf.Compression == PdfCompression.Lossless || page.ColorMode == ColorMode.Bw ? ExportFormat.Png : ExportFormat.Jpg;
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + (format == ExportFormat.Png ? ".png" : ".jpg"));
        FileStream file = File.Create(path);
        await using (file.ConfigureAwait(false))
        {
            await RenderAsync(page, Encoder(format, page.ColorMode, pdf.JpegQuality, forPdf: true), file, cancellationToken).ConfigureAwait(false);
        }

        return path;
    }

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

        // ImageSharp reports 96 dpi when metadata is absent, so the scan resolution is stamped unconditionally.
        ImageMetadata meta = image.Metadata;
        meta.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        meta.HorizontalResolution = page.Dpi;
        meta.VerticalResolution = page.Dpi;

        await image.SaveAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
    }

    // A grey JPEG is colour-encoded for a PDF: NAPS2 embeds that verbatim, but would decode and
    // re-encode a luminance-only one.
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
