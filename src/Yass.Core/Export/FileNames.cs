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

    /// <summary>Drops unsafe characters instead of replacing them, for names meant to read naturally.</summary>
    public static string Strip(string value) =>
        string.Join(' ', Unsafe().Replace(value ?? "", "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
