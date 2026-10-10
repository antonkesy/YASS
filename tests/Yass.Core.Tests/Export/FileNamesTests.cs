using Yass.Core.Export;

namespace Yass.Core.Tests.Export;

public sealed class FileNamesTests
{
    [Theory]
    [InlineData("Invoice 2026", "Invoice 2026")]
    [InlineData("../../etc/passwd", "-..-etc-passwd")]
    [InlineData(@"a\b:c*d?e""f<g>h|i", "a-b-c-d-e-f-g-h-i")]
    [InlineData("  trailing dots... ", "trailing dots")]
    [InlineData("...", "scan")]
    [InlineData("", "scan")]
    public void Safe_keeps_a_name_inside_its_folder_on_any_file_system(string name, string expected) =>
        Assert.Equal(expected, FileNames.Safe(name));

    [Fact]
    public void Unique_numbers_a_name_that_is_taken()
    {
        string dir = Directory.CreateTempSubdirectory("yass-names-").FullName;
        try
        {
            Assert.Equal(Path.Combine(dir, "scan.pdf"), FileNames.Unique(dir, "scan.pdf"));
            File.WriteAllText(Path.Combine(dir, "scan.pdf"), "");
            File.WriteAllText(Path.Combine(dir, "scan-2.pdf"), "");
            Assert.Equal(Path.Combine(dir, "scan-3.pdf"), FileNames.Unique(dir, "scan.pdf"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
