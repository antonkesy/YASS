using Yass.App.Export;

namespace Yass.Server.Tests.Export;

public sealed class DownloadPathsTests
{
    [Theory]
    [InlineData("", "/export/s1/abc.pdf")]
    [InlineData("/", "/export/s1/abc.pdf")]
    [InlineData("/scan", "/scan/export/s1/abc.pdf")]
    [InlineData("/scan/", "/scan/export/s1/abc.pdf")]
    public void Download_path_sits_under_the_base_path(string basePath, string expected) =>
        Assert.Equal(expected, new DownloadPaths(basePath).Download("s1", "abc", ".pdf"));
}
