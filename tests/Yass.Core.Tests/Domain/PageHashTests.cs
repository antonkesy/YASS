using Yass.Core.Domain;

namespace Yass.Core.Tests.Domain;

public class PageHashTests
{
    // Literal values from before RenderSpec existed. Every rendered image on disk is cached under
    // one of these, so an unedited page must keep hashing to exactly the same thing.
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
}
