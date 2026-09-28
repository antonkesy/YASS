using Yass.Core.Domain;

namespace Yass.Core.Tests.Domain;

public class PageEditsTests
{
    private static Session Session(int pages) => new(
        "s1",
        DateTimeOffset.UnixEpoch,
        SessionState.Review,
        Enumerable.Range(0, pages).Select(i => PageHash.Rehash(new Page(i, $"page-{i}.png", Rotation.R0, null, false, false, $"src{i}", ""), ColorMode.Grayscale)).ToArray(),
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
    public void SetColor_override_changes_hash_and_null_clears_it()
    {
        Session before = Session(1);

        Session colored = PageEdits.Apply(before, [PageEdit.SetColor(ColorMode.Color, 0)]);
        Session cleared = PageEdits.Apply(colored, [PageEdit.SetColor(null, 0)]);

        Assert.Equal(ColorMode.Color, colored.Pages[0].ColorOverride);
        Assert.NotEqual(before.Pages[0].Hash, colored.Pages[0].Hash);
        Assert.Null(cleared.Pages[0].ColorOverride);
        Assert.Equal(before.Pages[0].Hash, cleared.Pages[0].Hash);
    }

    [Fact]
    public void Override_equal_to_session_color_yields_same_hash_as_no_override()
    {
        Session before = Session(1);

        Session same = PageEdits.Apply(before, [PageEdit.SetColor(ColorMode.Grayscale, 0)]);

        Assert.Equal(before.Pages[0].Hash, same.Pages[0].Hash);
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
