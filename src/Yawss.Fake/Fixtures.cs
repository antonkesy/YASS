namespace Yawss.Fake;

/// <summary>PNG pages embedded in this assembly: three text pages, one colour page and one near-blank page.</summary>
public static class Fixtures
{
    public static IReadOnlyList<string> ContentPages { get; } = ["page-1.png", "page-2.png", "page-3.png", "page-4-color.png"];

    public const string BlankPage = "blank.png";

    public static byte[] Load(string name)
    {
        using Stream stream = typeof(Fixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded fixture '{name}' not found.", name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
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
