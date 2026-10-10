using Yass.Core.Domain;

namespace Yass.Core.Tests.Domain;

public class PageHashTests
{
    // Pre-RenderSpec literals: cached renders on disk are keyed by these, so unedited pages must hash identically.
    [Fact]
    public void An_unedited_page_keeps_the_hash_it_always_had()
    {
        Assert.Equal("da5ddadf068e83a3", PageHash.Derive("abc", new RenderSpec(Rotation.R0, PageFilter.Original, ColorMode.Grayscale, null)));
    }

    [Fact]
    public void A_rotated_filtered_cropped_page_keeps_the_hash_it_always_had()
    {
        var spec = new RenderSpec(Rotation.R90, PageFilter.Enhance, ColorMode.Color, new CropBox(10, 0, 0, 5));
        Assert.Equal("fd2d0c3f3a11b654", PageHash.Derive("abc", spec));
    }

    [Fact]
    public void An_empty_crop_hashes_like_no_crop()
    {
        Assert.Equal(
            PageHash.Derive("abc", new RenderSpec(Rotation.R0, PageFilter.Original, ColorMode.Color, null)),
            PageHash.Derive("abc", new RenderSpec(Rotation.R0, PageFilter.Original, ColorMode.Color, new CropBox(0, 0, 0, 0))));
    }

    [Fact]
    public void Straightening_leaves_no_empty_corners()
    {
        // A square turned 45 degrees fits a square 1/sqrt(2) of its size inside it.
        Assert.Equal(1 / Math.Sqrt(2), Straighten.InnerScale(100, 100, 45), 9);
        Assert.Equal(1, Straighten.InnerScale(200, 300, 0), 9);
        Assert.Equal(Straighten.InnerScale(200, 300, 5), Straighten.InnerScale(200, 300, -5), 9);
    }

    [Fact]
    public void A_tone_slider_maps_to_the_css_factor()
    {
        Assert.Equal(1f, PageAdjusts.Tone(0));
        Assert.Equal(0.5f, PageAdjusts.Tone(-100));
        Assert.Equal(1.5f, PageAdjusts.Tone(100));
    }
}
