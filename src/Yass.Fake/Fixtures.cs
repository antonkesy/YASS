using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Yass.Fake;

/// <summary>
/// Pages embedded in this assembly, rendered at build time from the Markdown in Fixtures/:
/// an invoice, a letter, a payslip, a report in colour, and a near-blank page. They read as
/// real documents on purpose — OCR and the naming that leans on it are only worth exercising
/// against something that has a title on it.
/// </summary>
public static class Fixtures
{
    public static IReadOnlyList<string> ContentPages { get; } = ["invoice.png", "letter.png", "payslip.png", "report-color.png"];

    public const string BlankPage = "blank.png";

    /// <summary>
    /// What the pages are rendered at, matching <c>scanner.defaults.dpi</c>. A scan asking for
    /// something else is resampled from this, the way a scanner would give you what you asked
    /// for rather than what its glass happens to be.
    /// </summary>
    public const int Dpi = 300;

    public static byte[] Load(string name)
    {
        using Stream stream = typeof(Fixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded fixture '{name}' not found.", name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// The page at the resolution the scan asked for. A page is only worth calling 300 dpi if
    /// it has the pixels for it: without this a 150 dpi scan would come back the same size as a
    /// 300 dpi one, and the PDF built from it would claim a page half the size of the paper.
    /// </summary>
    public static byte[] AtDpi(byte[] png, int dpi)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (dpi <= 0 || dpi == Dpi)
        {
            return png;
        }

        using Image image = Image.Load(png);
        image.Mutate(x => x.Resize(
            Math.Max(1, image.Width * dpi / Dpi),
            Math.Max(1, image.Height * dpi / Dpi)));

        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    /// <summary>Content page for a 1-based page number, cycling through the available fixtures.</summary>
    public static byte[] ContentPage(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return Load(ContentPages[(number - 1) % ContentPages.Count]);
    }

    public static string FixturePath(int number) => ContentPages[(number - 1) % ContentPages.Count];

    /// <summary>Writes every fixture into <paramref name="directory"/> for tests that need files on disk.</summary>
    public static IReadOnlyList<string> ExtractAll(string directory)
    {
        Directory.CreateDirectory(directory);
        var paths = new List<string>();
        foreach (string name in ContentPages.Append(BlankPage))
        {
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, Load(name));
            paths.Add(path);
        }

        return paths;
    }
}
