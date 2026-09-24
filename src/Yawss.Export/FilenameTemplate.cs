using System.Globalization;
using System.Text.RegularExpressions;

namespace Yawss.Export;

/// <summary>
/// Expands <c>output.filename_template</c>. Supported placeholders: <c>{date:FORMAT}</c>, <c>{time:FORMAT}</c>
/// (both .NET date formats over the same timestamp, kept separate for readability) and <c>{session}</c>.
/// The result is sanitised to a single safe file name.
/// </summary>
public static partial class FilenameTemplate
{
    [GeneratedRegex(@"\{(?<key>date|time|session)(?::(?<fmt>[^}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"[\x00-\x1f<>:""/\\|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();

    public static string Expand(string template, DateTimeOffset timestamp, string sessionId = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        DateTimeOffset local = timestamp.ToLocalTime();

        string expanded = Placeholder().Replace(template, m =>
        {
            string fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : "";
            return m.Groups["key"].Value switch
            {
                "date" => local.ToString(fmt.Length == 0 ? "yyyy-MM-dd" : fmt, CultureInfo.InvariantCulture),
                "time" => local.ToString(fmt.Length == 0 ? "HHmm" : fmt, CultureInfo.InvariantCulture),
                _ => sessionId,
            };
        });

        string name = Unsafe().Replace(expanded, "_").Trim().TrimStart('.');
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            name += ".pdf";
        }

        return name.Length <= 4 ? "scan.pdf" : name;
    }
}
