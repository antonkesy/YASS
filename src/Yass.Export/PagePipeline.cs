using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;

namespace Yass.Export;

/// <summary>The one place edits turn into pixels, shared by preview and export so they cannot drift apart.</summary>
public static class PagePipeline
{
    private const int ProbeSize = 512;

    private const double LevelsClip = 0.005;

    private const int LevelsMinSpan = 38;

    private const float SharpenReference = 2480f;

    /// <summary>
    /// Geometry first (crop, quarter turns, skew), then auto-levels, Enhance, user tone, despeckle before
    /// sharpen (else it sharpens the specks), and grayscale/threshold last.
    /// <para>maxWidth: previews shrink before the expensive skew and filters; every filter is per pixel or
    /// size-scaled, so the result looks the same. Null for export.</para>
    /// </summary>
    public static void Apply(Image image, RenderSpec spec, int? maxWidth = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(spec);

        image.Mutate(ctx =>
        {
            Turn(ctx, spec);
            if (maxWidth is { } width && ctx.GetCurrentSize().Width > width)
            {
                ctx.Resize(width, 0);
            }

            if (spec.Skew != 0)
            {
                Skew(ctx, spec.Skew);
            }
        });

        // The histogram needs the pixels at this point, so the pipeline runs as two Mutates around it.
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

    private static void Turn(IImageProcessingContext ctx, RenderSpec spec)
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
    }

    // Crops back to the page's proportions: filling the corners leaves wedges (white, or black once JPEG drops alpha).
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
