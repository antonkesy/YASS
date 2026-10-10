namespace Yass.Core.Domain;

/// <summary>
/// Everything that decides a page's pixels besides the source. Hash and render pipeline both read this
/// one record, so an input cannot reach the pipeline without also reaching the hash.
/// </summary>
public sealed record RenderSpec(
    Rotation Rotation,
    PageFilter Filter,
    ColorMode ScanColor,
    CropBox? Crop,
    PageAdjust? Adjust = null,
    PageCleanup Cleanup = PageCleanup.None,
    double Skew = 0)
{
    public ColorMode ColorMode => Filter.ColorFor(ScanColor);

    public static RenderSpec Of(Page page, ColorMode scanColor)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new RenderSpec(page.Rotation, page.Filter, scanColor, page.Crop, page.Adjust, page.Cleanup, page.Skew);
    }
}
