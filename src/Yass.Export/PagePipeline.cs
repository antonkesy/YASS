using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;

namespace Yass.Export;

/// <summary>
/// The one place a page's edits turn into pixels. Preview rendering and PDF export used to
/// carry separate copies of this, which is how they drift apart.
/// </summary>
public static class PagePipeline
{
    /// <summary>
    /// Crop, then rotate, then filter — the order the UI implies, since a crop is drawn on the
    /// upright page and rotation is a view on the result.
    /// </summary>
    public static void Apply(IImageProcessingContext ctx, Rotation rotation, PageFilter filter, ColorMode scanColor, CropBox? crop)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (crop is not null && !crop.IsEmpty())
        {
            Size size = ctx.GetCurrentSize();
            (int x, int y, int width, int height) = crop.ToPixels(size.Width, size.Height);
            ctx.Crop(new Rectangle(x, y, width, height));
        }

        if (rotation != Rotation.R0)
        {
            ctx.Rotate((float)(int)rotation);
        }

        if (filter.Enhances())
        {
            ctx.Contrast(PageFilters.EnhanceContrast)
                .Saturate(PageFilters.EnhanceSaturation)
                .Brightness(PageFilters.EnhanceBrightness);
        }

        switch (filter.ColorFor(scanColor))
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
