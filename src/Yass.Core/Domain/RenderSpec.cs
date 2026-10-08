namespace Yass.Core.Domain;

/// <summary>
/// Everything that decides a page's pixels, apart from the source file itself. The hash and the
/// render pipeline both read this one record, so an input that changes the image cannot reach the
/// pipeline without also reaching the hash — and a stale cached image cannot be served for it.
/// </summary>
/// <param name="Rotation">Quarter turns to apply after cropping.</param>
/// <param name="Filter">Per-page treatment.</param>
/// <param name="ScanColor">Colour mode the page was scanned in, which <see cref="PageFilter.Original"/> keeps.</param>
/// <param name="Crop">Trim to apply before rotating, or <c>null</c>.</param>
public sealed record RenderSpec(Rotation Rotation, PageFilter Filter, ColorMode ScanColor, CropBox? Crop)
{
    /// <summary>The colour mode the page actually renders in.</summary>
    public ColorMode ColorMode => Filter.ColorFor(ScanColor);

    public static RenderSpec Of(Page page, ColorMode scanColor)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new RenderSpec(page.Rotation, page.Filter, scanColor, page.Crop);
    }
}
