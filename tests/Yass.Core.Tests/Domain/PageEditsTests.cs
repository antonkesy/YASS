using Yass.Core.Domain;

namespace Yass.Core.Tests.Domain;

public class PageEditsTests
{
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
}
