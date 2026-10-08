using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;

namespace Yass.Export;

/// <summary>
/// The one place a page's edits turn into pixels. Preview rendering and PDF export used to
/// carry separate copies of this, which is how they drift apart.
/// </summary>
public static class PagePipeline
{
    /// <summary>Long edge of the copy auto-levels measures; plenty for a histogram, and cheap.</summary>
    private const int ProbeSize = 512;

    /// <summary>Share of the darkest and lightest pixels auto-levels ignores, so one speck does not set the range.</summary>
    private const double LevelsClip = 0.005;

    /// <summary>A page whose tones span less than this is blank, and stretching it would only amplify noise.</summary>
    private const int LevelsMinSpan = 38;

    /// <summary>Short edge of an A4 page at 300 dpi; sharpening is tuned for it and scaled for anything else.</summary>
    private const float SharpenReference = 2480f;

    /// <summary>
    /// Geometry first — crop on the scan as it came, the quarter turns, then the fine skew — then
    /// tone, clean-up and colour in the order each needs the one before it:
    /// <list type="number">
    /// <item>auto-levels normalises the range, so everything after works on the same footing;</item>
    /// <item>the Enhance filter, then the user's own brightness and contrast, which fine-tune what Enhance did;</item>
    /// <item>despeckle before sharpen, which would otherwise sharpen the specks;</item>
    /// <item>grayscale or the black-and-white threshold last, on the cleaned page.</item>
    /// </list>
    /// </summary>
    public static void Apply(Image image, RenderSpec spec)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(spec);

        image.Mutate(ctx => Geometry(ctx, spec));

        // Measuring the histogram needs the pixels as they are at this point, which a single
        // Mutate cannot give it, so the pipeline runs in two halves around it.
        ColorMatrix? levels = spec.Cleanup.HasFlag(PageCleanup.AutoLevels) ? Levels(image) : null;

        image.Mutate(ctx =>
        {
            if (levels is { } matrix)
            {
                ctx.Filter(matrix);
            }

            Tone(ctx, spec);
        });
    }

    private static void Geometry(IImageProcessingContext ctx, RenderSpec spec)
    {
        if (spec.Crop is { } crop && !crop.IsEmpty())
        {
            Size size = ctx.GetCurrentSize();
            (int x, int y, int width, int height) = crop.ToPixels(size.Width, size.Height);
            ctx.Crop(new Rectangle(x, y, width, height));
        }

        if (spec.Rotation != Rotation.R0)
        {
            ctx.Rotate((int)spec.Rotation);
        }

        if (spec.Skew != 0)
        {
            Skew(ctx, spec.Skew);
        }
    }

    /// <summary>
    /// Turns the page by a small angle and crops back to its own proportions inside the turned
    /// canvas. Filling the corners instead leaves wedges — white on a grey scan, black once a
    /// JPEG has dropped the transparency — and every page of a document would have them.
    /// </summary>
    private static void Skew(IImageProcessingContext ctx, double degrees)
    {
        Size before = ctx.GetCurrentSize();
        double scale = Straighten.InnerScale(before.Width, before.Height, degrees);
        ctx.Rotate((float)degrees);

        Size turned = ctx.GetCurrentSize();
        // A pixel in from the exact fit: the rotation antialiases the canvas edge into the page.
        int width = Math.Max(1, (int)Math.Floor(before.Width * scale) - 2);
        int height = Math.Max(1, (int)Math.Floor(before.Height * scale) - 2);
        ctx.Crop(new Rectangle((turned.Width - width) / 2, (turned.Height - height) / 2, width, height));
    }

    /// <summary>
    /// The stretch that maps the page's darkest ink to black and its paper to white, measured on a
    /// small grey copy. Null when the page has no range worth stretching.
    /// </summary>
    private static ColorMatrix? Levels(Image image)
    {
        using Image<L8> probe = image.CloneAs<L8>();
        probe.Mutate(ctx => ctx.Resize(new ResizeOptions { Size = new Size(ProbeSize, ProbeSize), Mode = ResizeMode.Max }));

        int[] histogram = new int[256];
        probe.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < rows.Height; y++)
            {
                foreach (L8 pixel in rows.GetRowSpan(y))
                {
                    histogram[pixel.PackedValue]++;
                }
            }
        });

        (int lo, int hi) = Percentiles(histogram, probe.Width * probe.Height);
        if (hi - lo < LevelsMinSpan)
        {
            return null;
        }

        float scale = 255f / (hi - lo);
        float offset = -lo / (float)(hi - lo);
        return new ColorMatrix(
            scale, 0, 0, 0,
            0, scale, 0, 0,
            0, 0, scale, 0,
            0, 0, 0, 1,
            offset, offset, offset, 0);
    }

    internal static (int Lo, int Hi) Percentiles(int[] histogram, int total)
    {
        long clip = (long)Math.Floor(total * LevelsClip);
        int lo = 0;
        for (long seen = 0; lo < 255 && (seen += histogram[lo]) <= clip; lo++)
        {
        }

        int hi = 255;
        for (long seen = 0; hi > 0 && (seen += histogram[hi]) <= clip; hi--)
        {
        }

        return (lo, hi);
    }

    private static void Tone(IImageProcessingContext ctx, RenderSpec spec)
    {
        if (spec.Filter.Enhances())
        {
            ctx.Contrast(PageFilters.EnhanceContrast)
                .Saturate(PageFilters.EnhanceSaturation)
                .Brightness(PageFilters.EnhanceBrightness);
        }

        if (spec.Adjust is { } adjust && !adjust.IsNeutral())
        {
            if (adjust.Brightness != 0)
            {
                ctx.Brightness(PageAdjusts.Tone(adjust.Brightness));
            }

            if (adjust.Contrast != 0)
            {
                ctx.Contrast(PageAdjusts.Tone(adjust.Contrast));
            }
        }

        if (spec.Cleanup.HasFlag(PageCleanup.Despeckle))
        {
            ctx.MedianBlur(1, preserveAlpha: true);
        }

        if (spec.Adjust is { Sharpen: > 0 } sharp)
        {
            Size size = ctx.GetCurrentSize();
            float resolution = Math.Max(0.25f, Math.Min(size.Width, size.Height) / SharpenReference);
            ctx.GaussianSharpen((0.4f + (2.6f * sharp.Sharpen / PageAdjust.MaxSharpen)) * resolution);
        }

        switch (spec.ColorMode)
        {
            case ColorMode.Grayscale:
                ctx.Grayscale();
                break;
            case ColorMode.Bw:
                ctx.Grayscale().BinaryThreshold(0.55f);
                break;
            case ColorMode.Color:
            default:
                break;
        }
    }
}
