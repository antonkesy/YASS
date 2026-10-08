using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yass.Core.Domain;

namespace Yass.Export.Tests;

public class PagePipelineTests
{
    private static Image<Rgb24> Page(int width = 200, int height = 300, byte shade = 200) =>
        new(width, height, new Rgb24(shade, shade, shade));

    private static RenderSpec Spec(Rotation rotation = Rotation.R0, PageFilter filter = PageFilter.Original, CropBox? crop = null) =>
        new(rotation, filter, ColorMode.Color, crop);

    [Fact]
    public void Crops_in_source_coordinates_then_rotates()
    {
        using Image<Rgb24> image = Page();

        PagePipeline.Apply(image, Spec(Rotation.R90, crop: new CropBox(10, 0, 0, 25)));

        // 200x300 less 25% of the width and 10% of the height is 150x270, then turned on its side.
        Assert.Equal(270, image.Width);
        Assert.Equal(150, image.Height);
    }

    [Fact]
    public void Leaves_an_unedited_page_alone()
    {
        using Image<Rgb24> image = Page(shade: 123);

        PagePipeline.Apply(image, Spec());

        Assert.Equal(new Rgb24(123, 123, 123), image[10, 10]);
        Assert.Equal((200, 300), (image.Width, image.Height));
    }

    [Fact]
    public void Black_and_white_thresholds_every_pixel()
    {
        using Image<Rgb24> image = Page(shade: 100);
        image[0, 0] = new Rgb24(230, 230, 230);

        PagePipeline.Apply(image, Spec(filter: PageFilter.Bw));

        Assert.Equal(new Rgb24(0, 0, 0), image[10, 10]);
        Assert.Equal(new Rgb24(255, 255, 255), image[0, 0]);
    }
}
