using System.Globalization;
using System.Text.RegularExpressions;

namespace Yass.Core.Export;

// Not Path.GetInvalidFileNameChars: that is platform-dependent, so a name made on Linux
// could be unusable on a Windows client or a mounted share.
public static partial class FileNames
{
    [GeneratedRegex(@"[\x00-\x1f<>:""/\\|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();

    public static string Sanitize(string value, string replacement = "_") =>
        Unsafe().Replace(value ?? "", replacement).Trim().TrimStart('.');

    public static string Safe(string name)
    {
        string safe = Sanitize(name, "-").TrimEnd('.').TrimEnd();
        return safe.Length == 0 ? "scan" : safe;
    }

    public static string Unique(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem}-{n.ToString(CultureInfo.InvariantCulture)}{extension}");
        }

        return candidate;
    }

    public static string Strip(string value) =>
        string.Join(' ', Unsafe().Replace(value ?? "", "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
