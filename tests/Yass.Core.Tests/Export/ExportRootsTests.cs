using Yass.Core.Export;

namespace Yass.Core.Tests.Export;

public sealed class ExportRootsTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("yass-roots").FullName;

    [Fact]
    public void Falls_back_to_the_output_directory_when_no_roots_are_configured()
    {
        Assert.Equal(["/data/consume"], ExportRoots.Effective([], "/data/consume"));
        Assert.Equal(["/data/consume"], ExportRoots.Effective(null, "/data/consume"));
        Assert.Equal(["/data/consume"], ExportRoots.Effective(["  ", ""], "/data/consume"));
    }

    [Fact]
    public void Uses_the_configured_roots_when_there_are_any()
    {
        Assert.Equal(["/data", "/mnt/nas"], ExportRoots.Effective([" /data ", "/mnt/nas"], "/data/consume"));
    }

    [Fact]
    public void Allows_a_root_itself_and_anything_beneath_it()
    {
        string[] roots = [Path.Combine(_temp, "data")];

        Assert.True(ExportRoots.Allows(roots, Path.Combine(_temp, "data")));
        Assert.True(ExportRoots.Allows(roots, Path.Combine(_temp, "data", "consume")));
        Assert.True(ExportRoots.Allows(roots, Path.Combine(_temp, "data", "a", "b", "c")));
        Assert.True(ExportRoots.Allows(roots, Path.Combine(_temp, "data") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Refuses_a_sibling_whose_name_merely_starts_the_same()
    {
        // "/data-other" is not inside "/data"; a plain StartsWith would say it is.
        string[] roots = [Path.Combine(_temp, "data")];

        Assert.False(ExportRoots.Allows(roots, Path.Combine(_temp, "data-other")));
        Assert.False(ExportRoots.Allows(roots, Path.Combine(_temp, "database")));
    }

    [Fact]
    public void Refuses_a_path_that_climbs_out_with_dot_dot()
    {
        string[] roots = [Path.Combine(_temp, "data")];

        Assert.False(ExportRoots.Allows(roots, Path.Combine(_temp, "data", "..", "elsewhere")));
        Assert.False(ExportRoots.Allows(roots, Path.Combine(_temp, "data", "..", "..", "etc")));
    }

    [Fact]
    public void Refuses_a_symlink_inside_a_root_that_points_outside_it()
    {
        // The whole reason containment resolves links: this path looks contained and is not.
        string root = Path.Combine(_temp, "data");
        string outside = Path.Combine(_temp, "elsewhere");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        string link = Path.Combine(root, "escape");
        Directory.CreateSymbolicLink(link, outside);

        Assert.False(ExportRoots.Allows([root], link));
        Assert.False(ExportRoots.Allows([root], Path.Combine(link, "deeper")));
    }

    [Fact]
    public void Follows_a_symlinked_root_to_the_same_place_as_the_real_one()
    {
        string real = Path.Combine(_temp, "real");
        Directory.CreateDirectory(real);
        string linkedRoot = Path.Combine(_temp, "linked");
        Directory.CreateSymbolicLink(linkedRoot, real);

        Assert.True(ExportRoots.Allows([linkedRoot], Path.Combine(real, "inside")));
        Assert.True(ExportRoots.Allows([real], Path.Combine(linkedRoot, "inside")));
    }

    [Fact]
    public void Root_slash_allows_anywhere_for_whoever_wants_that()
    {
        Assert.True(ExportRoots.Allows(["/"], "/anywhere/at/all"));
        Assert.True(ExportRoots.Allows(["/"], _temp));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}
