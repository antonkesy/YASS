using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Yass.Fake;

/// <summary>Rendered at build time from Fixtures/*.md (SPEC.md §10). The order is the demo scan's feed order, chosen on purpose.</summary>
public static class Fixtures
{
    public static IReadOnlyList<string> ContentPages { get; } =
    [
        "invoice.png",
        "letter.png",
        "payslip.png",
        "report-color.png",
        "bank-statement.png",
        "receipt.png",
        "utility-bill.png",
        "insurance-letter.png",
        "contract-1.png",
        "contract-2.png",
        "contract-3.png",
        "table-dense.png",
        "schedule-landscape.png",
        "form.png",
        "faded.png",
        "rechnung.png",
    ];

    public const string BlankPage = "blank.png";

    public const int Dpi = 300;

    public static byte[] Load(string name)
    {
        using Stream stream = typeof(Fixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded fixture '{name}' not found.", name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    // Resampled so a 150 dpi scan has half the pixels; otherwise the PDF would claim a page half the paper's size.
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

    public static byte[] ContentPage(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return Load(ContentPages[(number - 1) % ContentPages.Count]);
    }

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
