using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;

namespace Yass.Export.Tests;

public class PagePipelineTests
{
    private static Image<Rgb24> Page(int width = 200, int height = 300, byte shade = 200) =>
        new(width, height, new Rgb24(shade, shade, shade));

    private static RenderSpec Spec(
        Rotation rotation = Rotation.R0,
        PageFilter filter = PageFilter.Original,
        CropBox? crop = null,
        PageAdjust? adjust = null,
        PageCleanup cleanup = PageCleanup.None,
        double skew = 0) =>
        new(rotation, filter, ColorMode.Color, crop, adjust, cleanup, skew);

    private static byte Grey(Image<Rgb24> image, int x, int y) => image[x, y].R;

    private static (byte Min, byte Max) Range(Image<Rgb24> image)
    {
        byte min = 255, max = 0;
        image.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < rows.Height; y++)
            {
                foreach (Rgb24 p in rows.GetRowSpan(y))
                {
                    min = Math.Min(min, p.R);
                    max = Math.Max(max, p.R);
                }
            }
        });
        return (min, max);
    }

    /// <summary>A washed-out page: a left-to-right ramp from 80 to 170, nothing near black or white.</summary>
    private static Image<Rgb24> Faded()
    {
        var image = new Image<Rgb24>(200, 100);
        image.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < rows.Height; y++)
            {
                Span<Rgb24> row = rows.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    byte v = (byte)(80 + (x * 90 / 199));
                    row[x] = new Rgb24(v, v, v);
                }
            }
        });
        return image;
    }

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

    [Fact]
    public void Neutral_new_edits_change_nothing()
    {
        using Image<Rgb24> image = Page(shade: 123);

        PagePipeline.Apply(image, Spec(adjust: new PageAdjust(0, 0, 0)));

        Assert.Equal(new Rgb24(123, 123, 123), image[10, 10]);
    }

    [Fact]
    public void Skew_crops_back_to_the_page_with_no_empty_corners()
    {
        using Image<Rgb24> image = Page(200, 300, shade: 200);

        PagePipeline.Apply(image, Spec(skew: 10));

        double scale = Straighten.InnerScale(200, 300, 10);
        Assert.InRange(image.Width, (int)(200 * scale) - 3, (int)(200 * scale));
        Assert.InRange(image.Height, (int)(300 * scale) - 3, (int)(300 * scale));
        foreach ((int x, int y) in new[] { (0, 0), (image.Width - 1, 0), (0, image.Height - 1), (image.Width - 1, image.Height - 1) })
        {
            Assert.InRange(Grey(image, x, y), 190, 210);
        }
    }

    [Fact]
    public void Auto_levels_stretches_a_faded_page_to_black_and_white()
    {
        using Image<Rgb24> image = Faded();

        PagePipeline.Apply(image, Spec(cleanup: PageCleanup.AutoLevels));

        (byte min, byte max) = Range(image);
        Assert.InRange(min, 0, 10);
        Assert.InRange(max, 245, 255);
    }

    [Fact]
    public void Auto_levels_leaves_a_blank_page_alone()
    {
        using Image<Rgb24> image = Page(shade: 200);
        image[5, 5] = new Rgb24(190, 190, 190);

        PagePipeline.Apply(image, Spec(cleanup: PageCleanup.AutoLevels));

        Assert.Equal(200, Grey(image, 10, 10));
    }

    [Fact]
    public void Auto_levels_ignores_a_single_speck_when_finding_the_range()
    {
        int[] histogram = new int[256];
        histogram[0] = 1;
        histogram[100] = 9_000;
        histogram[200] = 9_000;
        histogram[255] = 1;

        Assert.Equal((100, 200), PagePipeline.Percentiles(histogram, 18_002));
    }

    [Fact]
    public void Despeckle_removes_a_lone_dot()
    {
        using Image<Rgb24> image = Page(shade: 255);
        image[50, 50] = new Rgb24(0, 0, 0);

        PagePipeline.Apply(image, Spec(cleanup: PageCleanup.Despeckle));

        Assert.Equal(255, Grey(image, 50, 50));
    }

    [Fact]
    public void Brightness_and_contrast_move_the_tones_the_way_their_sliders_say()
    {
        using Image<Rgb24> brighter = Page(shade: 100);
        using Image<Rgb24> darker = Page(shade: 100);
        using Image<Rgb24> flatter = Faded();

        PagePipeline.Apply(brighter, Spec(adjust: new PageAdjust(50, 0, 0)));
        PagePipeline.Apply(darker, Spec(adjust: new PageAdjust(-50, 0, 0)));
        PagePipeline.Apply(flatter, Spec(adjust: new PageAdjust(0, -60, 0)));

        Assert.True(Grey(brighter, 10, 10) > 100);
        Assert.True(Grey(darker, 10, 10) < 100);
        (byte min, byte max) = Range(flatter);
        Assert.True(max - min < 90);
    }

    [Fact]
    public void Sharpen_makes_an_edge_steeper()
    {
        using Image<Rgb24> image = Page(shade: 255);
        image.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < rows.Height; y++)
            {
                rows.GetRowSpan(y)[100..].Fill(new Rgb24(0, 0, 0));
            }
        });
        image.Mutate(ctx => ctx.GaussianBlur(2));
        byte before = Grey(image, 98, 150);

        PagePipeline.Apply(image, Spec(adjust: new PageAdjust(0, 0, 100)));

        Assert.True(Grey(image, 98, 150) > before);
    }
}
