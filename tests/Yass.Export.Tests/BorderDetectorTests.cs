using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Yass.Export.Tests;

public class BorderDetectorTests
{
    private static Image<L8> Scan(int width, int height, int top = 0, int right = 0, int bottom = 0, int left = 0, byte frame = 30)
    {
        var image = new Image<L8>(width, height, new L8(240));
        image.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < rows.Height; y++)
            {
                Span<L8> row = rows.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (y < top || y >= height - bottom || x < left || x >= width - right)
                    {
                        row[x] = new L8(frame);
                    }
                }
            }
        });
        return image;
    }

    [Fact]
    public void Finds_a_frame_all_round_and_keeps_a_small_margin()
    {
        using Image<L8> scan = Scan(400, 600, top: 30, right: 20, bottom: 60, left: 40);

        var box = BorderDetector.Detect(scan);

        Assert.NotNull(box);
        Assert.InRange(box.Top, 5, 6);      // 30 of 600 is 5%
        Assert.InRange(box.Bottom, 10, 11); // 60 of 600 is 10%
        Assert.InRange(box.Left, 10, 11);   // 40 of 400 is 10%
        Assert.InRange(box.Right, 5, 6);    // 20 of 400 is 5%
    }

    [Fact]
    public void Finds_a_frame_down_one_side_only()
    {
        using Image<L8> scan = Scan(400, 600, right: 48);

        var box = BorderDetector.Detect(scan);

        Assert.NotNull(box);
        Assert.Equal(0, box.Top);
        Assert.Equal(0, box.Left);
        Assert.Equal(0, box.Bottom);
        Assert.InRange(box.Right, 12, 13);
    }

    [Fact]
    public void Finds_nothing_on_a_clean_page()
    {
        using Image<L8> scan = Scan(400, 600);

        Assert.Null(BorderDetector.Detect(scan));
    }

    [Fact]
    public void Leaves_a_page_that_is_dark_all_over_alone()
    {
        using var scan = new Image<L8>(400, 600, new L8(40));

        Assert.Null(BorderDetector.Detect(scan));
    }

    [Fact]
    public void Never_takes_more_of_an_edge_than_the_crop_dialog_can()
    {
        using Image<L8> scan = Scan(400, 600, left: 180);

        var box = BorderDetector.Detect(scan);

        Assert.NotNull(box);
        Assert.Equal(BorderDetector.MaxEdgePercent, box.Left);
    }

    [Fact]
    public void Ignores_text_on_the_page()
    {
        using Image<L8> scan = Scan(400, 600, top: 30);
        scan.ProcessPixelRows(rows =>
        {
            for (int y = 100; y < 110; y++)
            {
                rows.GetRowSpan(y)[10..150].Fill(new L8(10));
            }
        });

        var box = BorderDetector.Detect(scan);

        Assert.NotNull(box);
        Assert.Equal(0, box.Left);
        Assert.InRange(box.Top, 5, 6);
    }

    [Fact]
    public void Works_on_a_full_size_colour_scan()
    {
        using var scan = new Image<Rgb24>(2480, 3508, new Rgb24(235, 235, 235));
        scan.ProcessPixelRows(rows =>
        {
            for (int y = 0; y < 175; y++)
            {
                rows.GetRowSpan(y).Fill(new Rgb24(20, 20, 20));
            }
        });

        var box = BorderDetector.Detect(scan);

        Assert.NotNull(box);
        Assert.InRange(box.Top, 4.5, 6);
    }
}
