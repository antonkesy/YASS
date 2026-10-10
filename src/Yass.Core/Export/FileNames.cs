using System.Globalization;
using System.Text.RegularExpressions;

namespace Yass.Core.Export;

/// <summary>
/// What may appear in a file name YASS writes. Deliberately not
/// <see cref="Path.GetInvalidFileNameChars"/>: that is platform-dependent, so a name
/// suggested on Linux could be unusable on a Windows client or a mounted share.
/// </summary>
public static partial class FileNames
{
    [GeneratedRegex(@"[\x00-\x1f<>:""/\\|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();

    /// <summary>Replaces anything unsafe with <paramref name="replacement"/> and trims the result.</summary>
    public static string Sanitize(string value, string replacement = "_") =>
        Unsafe().Replace(value ?? "", replacement).Trim().TrimStart('.');

    /// <summary>
    /// A whole file name made safe to write into a folder: no path separators or characters some
    /// file system refuses, so it can never climb out of the folder, and never empty.
    /// </summary>
    public static string Safe(string name)
    {
        string safe = Sanitize(name, "-").TrimEnd('.').TrimEnd();
        return safe.Length == 0 ? "scan" : safe;
    }

    /// <summary>The name in <paramref name="directory"/>, or with -2, -3… before the extension: never an existing file.</summary>
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

    /// <summary>Drops unsafe characters instead of replacing them, for names meant to read naturally.</summary>
    public static string Strip(string value) =>
        string.Join(' ', Unsafe().Replace(value ?? "", "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
