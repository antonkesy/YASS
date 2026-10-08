using System.Text.Json;
using System.Text.Json.Serialization;
using Yass.Core.Domain;

namespace Yass.Core.Tests.Domain;

public class PageEditsTests
{
    // The options SessionStore writes session.json with.
    private static readonly JsonSerializerOptions SessionJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static Session Session(int pages) => new(
        "s1",
        DateTimeOffset.UnixEpoch,
        SessionState.Review,
        Enumerable.Range(0, pages).Select(i => PageHash.Rehash(new Page(i, $"page-{i}.png", Rotation.R0, PageFilter.Original, null, false, false, false, $"src{i}", ""), ColorMode.Grayscale)).ToArray(),
        new ScanOptions());

    [Fact]
    public void Rotate_targets_only_named_pages_and_changes_their_hash()
    {
        Session before = Session(3);

        Session after = PageEdits.Apply(before, [PageEdit.Rotate(90, 1)]);

        Assert.Equal(Rotation.R90, after.Pages[1].Rotation);
        Assert.Equal(Rotation.R0, after.Pages[0].Rotation);
        Assert.NotEqual(before.Pages[1].Hash, after.Pages[1].Hash);
        Assert.Equal(before.Pages[0].Hash, after.Pages[0].Hash);
    }

    [Fact]
    public void Empty_targets_means_all_pages()
    {
        Session after = PageEdits.Apply(Session(3), [PageEdit.Rotate(-90)]);

        Assert.All(after.Pages, p => Assert.Equal(Rotation.R270, p.Rotation));
    }

    [Fact]
    public void Rotation_accumulates_and_wraps()
    {
        Session after = PageEdits.Apply(Session(1), [PageEdit.Rotate(90, 0), PageEdit.Rotate(90, 0), PageEdit.Rotate(180, 0)]);

        Assert.Equal(Rotation.R0, after.Pages[0].Rotation);
    }

    [Fact]
    public void Delete_and_restore_toggle_without_touching_hash()
    {
        Session before = Session(2);

        Session deleted = PageEdits.Apply(before, [PageEdit.Delete(0)]);
        Session restored = PageEdits.Apply(deleted, [PageEdit.Restore(0)]);

        Assert.True(deleted.Pages[0].Deleted);
        Assert.Single(deleted.ActivePages);
        Assert.False(restored.Pages[0].Deleted);
        Assert.Equal(before.Pages[0].Hash, deleted.Pages[0].Hash);
    }

    [Fact]
    public void SetFilter_changes_hash_and_original_restores_it()
    {
        Session before = Session(1);

        Session bw = PageEdits.Apply(before, [PageEdit.SetFilter(PageFilter.Bw, 0)]);
        Session back = PageEdits.Apply(bw, [PageEdit.SetFilter(PageFilter.Original, 0)]);

        Assert.Equal(PageFilter.Bw, bw.Pages[0].Filter);
        Assert.NotEqual(before.Pages[0].Hash, bw.Pages[0].Hash);
        Assert.Equal(PageFilter.Original, back.Pages[0].Filter);
        Assert.Equal(before.Pages[0].Hash, back.Pages[0].Hash);
    }

    [Fact]
    public void Grayscale_filter_on_a_grayscale_scan_still_gets_its_own_hash()
    {
        // The rendered pixels match, but the two are different edits and a later change to
        // what Grayscale means must not be served from the Original cache entry.
        Session before = Session(1);

        Session same = PageEdits.Apply(before, [PageEdit.SetFilter(PageFilter.Grayscale, 0)]);

        Assert.NotEqual(before.Pages[0].Hash, same.Pages[0].Hash);
    }

    [Fact]
    public void Enhance_keeps_the_scan_colour_but_changes_the_hash()
    {
        Session before = Session(1);

        Session enhanced = PageEdits.Apply(before, [PageEdit.SetFilter(PageFilter.Enhance, 0)]);

        Assert.Equal(ColorMode.Grayscale, PageFilter.Enhance.ColorFor(ColorMode.Grayscale));
        Assert.NotEqual(before.Pages[0].Hash, enhanced.Pages[0].Hash);
    }

    [Fact]
    public void SetCrop_changes_hash_and_an_empty_box_clears_it()
    {
        Session before = Session(1);

        Session cropped = PageEdits.Apply(before, [PageEdit.SetCrop(new CropBox(5, 10, 5, 10), 0)]);
        Session cleared = PageEdits.Apply(cropped, [PageEdit.SetCrop(null, 0)]);

        Assert.Equal(new CropBox(5, 10, 5, 10), cropped.Pages[0].Crop);
        Assert.NotEqual(before.Pages[0].Hash, cropped.Pages[0].Hash);
        Assert.Null(cleared.Pages[0].Crop);
        Assert.Equal(before.Pages[0].Hash, cleared.Pages[0].Hash);
    }

    [Fact]
    public void A_crop_that_would_leave_nothing_is_rejected()
    {
        Session before = Session(1);

        Assert.Throws<ArgumentException>(() => PageEdits.Apply(before, [PageEdit.SetCrop(new CropBox(60, 0, 60, 0), 0)]));
        Assert.Throws<ArgumentException>(() => PageEdits.Apply(before, [PageEdit.SetCrop(new CropBox(-1, 0, 0, 0), 0)]));
    }

    [Fact]
    public void Reorder_moves_pages_and_reindexes()
    {
        Session before = Session(3);

        Session after = PageEdits.Apply(before, [PageEdit.Reorder(2, 0, 1)]);

        Assert.Equal(["page-2.png", "page-0.png", "page-1.png"], after.Pages.Select(p => p.SourceFile));
        Assert.Equal([0, 1, 2], after.Pages.Select(p => p.Index));
        Assert.Equal(before.Pages[2].Hash, after.Pages[0].Hash);
    }

    [Theory]
    [InlineData("0,1")]
    [InlineData("0,0,1")]
    [InlineData("0,1,3")]
    public void Reorder_rejects_non_permutations(string order) =>
        Assert.Throws<ArgumentException>(() => PageEdits.Apply(Session(3), [PageEdit.Reorder(order.Split(',').Select(int.Parse).ToArray())]));

    [Fact]
    public void Out_of_range_target_is_rejected() =>
        Assert.Throws<ArgumentException>(() => PageEdits.Apply(Session(2), [PageEdit.Delete(2)]));

    [Fact]
    public void Non_multiple_of_90_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PageEdits.Apply(Session(1), [PageEdit.Rotate(45, 0)]));

    [Fact]
    public void Edits_apply_in_order()
    {
        Session after = PageEdits.Apply(Session(2), [PageEdit.Reorder(1, 0), PageEdit.Delete(0)]);

        Assert.Equal("page-1.png", after.Pages[0].SourceFile);
        Assert.True(after.Pages[0].Deleted);
        Assert.False(after.Pages[1].Deleted);
    }

    [Fact]
    public void Adjust_is_set_per_page_and_a_neutral_one_is_stored_as_none()
    {
        Session before = Session(2);

        Session adjusted = PageEdits.Apply(before, [PageEdit.SetAdjust(new PageAdjust(20, -10, 30), 1)]);
        Session cleared = PageEdits.Apply(adjusted, [PageEdit.SetAdjust(new PageAdjust(0, 0, 0), 1)]);

        Assert.Equal(new PageAdjust(20, -10, 30), adjusted.Pages[1].Adjust);
        Assert.Null(adjusted.Pages[0].Adjust);
        Assert.NotEqual(before.Pages[1].Hash, adjusted.Pages[1].Hash);
        Assert.Null(cleared.Pages[1].Adjust);
        Assert.Equal(before.Pages[1].Hash, cleared.Pages[1].Hash);
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(0, -101, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 101)]
    public void An_adjust_out_of_range_is_rejected(int brightness, int contrast, int sharpen)
    {
        Assert.Throws<ArgumentException>(() => PageEdits.Apply(Session(1), [PageEdit.SetAdjust(new PageAdjust(brightness, contrast, sharpen), 0)]));
    }

    [Fact]
    public void Cleanup_passes_are_turned_on_and_off_independently()
    {
        Session before = Session(1);

        Session both = PageEdits.Apply(before, [PageEdit.AddCleanup(PageCleanup.Despeckle, 0), PageEdit.AddCleanup(PageCleanup.AutoLevels, 0)]);
        Session again = PageEdits.Apply(both, [PageEdit.AddCleanup(PageCleanup.Despeckle, 0)]);
        Session one = PageEdits.Apply(both, [PageEdit.RemoveCleanup(PageCleanup.Despeckle, 0)]);
        Session none = PageEdits.Apply(one, [PageEdit.RemoveCleanup(PageCleanup.AutoLevels, 0)]);

        Assert.Equal(PageCleanup.Despeckle | PageCleanup.AutoLevels, both.Pages[0].Cleanup);
        Assert.Equal(both.Pages[0], again.Pages[0]);
        Assert.Equal(PageCleanup.AutoLevels, one.Pages[0].Cleanup);
        Assert.NotEqual(both.Pages[0].Hash, one.Pages[0].Hash);
        Assert.Equal(before.Pages[0].Hash, none.Pages[0].Hash);
    }

    [Fact]
    public void Skew_is_rounded_to_a_tenth_of_a_degree()
    {
        Session before = Session(1);

        Session skewed = PageEdits.Apply(before, [PageEdit.SetSkew(2.34, 0)]);
        Session same = PageEdits.Apply(before, [PageEdit.SetSkew(2.3, 0)]);
        Session straight = PageEdits.Apply(skewed, [PageEdit.SetSkew(0.04, 0)]);

        Assert.Equal(2.3, skewed.Pages[0].Skew);
        Assert.Equal(same.Pages[0].Hash, skewed.Pages[0].Hash);
        Assert.NotEqual(before.Pages[0].Hash, skewed.Pages[0].Hash);
        Assert.Equal(0, straight.Pages[0].Skew);
        Assert.Equal(before.Pages[0].Hash, straight.Pages[0].Hash);
    }

    [Theory]
    [InlineData(15.1)]
    [InlineData(-20)]
    [InlineData(double.NaN)]
    public void A_skew_out_of_range_is_rejected(double degrees)
    {
        Assert.Throws<ArgumentException>(() => PageEdits.Apply(Session(1), [PageEdit.SetSkew(degrees, 0)]));
    }

    [Fact]
    public void A_page_saved_before_adjust_cleanup_and_skew_existed_loads_without_them()
    {
        const string Old = """
            {"index":0,"sourceFile":"p.png","rotation":"R90","filter":"Enhance","crop":null,"startsDocument":false,
             "deleted":false,"isBlank":false,"sourceHash":"abc","hash":"fd2d0c3f3a11b654"}
            """;

        Page page = JsonSerializer.Deserialize<Page>(Old, SessionJson)!;

        Assert.Null(page.Adjust);
        Assert.Equal(PageCleanup.None, page.Cleanup);
        Assert.Equal(0, page.Skew);
        Assert.Equal(
            PageHash.Derive("abc", new RenderSpec(Rotation.R90, PageFilter.Enhance, ColorMode.Color, null)),
            PageHash.Rehash(page, ColorMode.Color).Hash);
    }

    [Fact]
    public void Cleanup_flags_round_trip_through_the_session_file_format()
    {
        Page page = Session(1).Pages[0] with { Cleanup = PageCleanup.Despeckle | PageCleanup.AutoLevels, Adjust = new PageAdjust(5, 0, 10), Skew = -1.5 };

        Page back = JsonSerializer.Deserialize<Page>(JsonSerializer.Serialize(page, SessionJson), SessionJson)!;

        Assert.Equal(page, back);
    }
}
