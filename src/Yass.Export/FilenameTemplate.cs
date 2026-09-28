using System.Globalization;
using System.Text.RegularExpressions;

namespace Yass.Export;

/// <summary>
/// Expands <c>output.filename_template</c>. Two syntaxes are supported side by side:
/// the original <c>{date:FORMAT}</c>, <c>{time:FORMAT}</c> and <c>{session}</c>, and the
/// UI's plain tokens (<c>{YEAR}</c>, <c>{MONTH}</c>, <c>{PRESET}</c>, …). Existing config
/// files keep working; the editor in the settings sheet writes the second kind.
/// The result is sanitised to a single safe file name.
/// </summary>
public static partial class FilenameTemplate
{
    [GeneratedRegex(@"\{(?<key>[A-Za-z]+)(?::(?<fmt>[^}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"[\x00-\x1f<>:""/\\|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();

    /// <summary>The tokens the settings editor offers, for its chip row and its validation.</summary>
    public static readonly IReadOnlyList<string> Tokens =
    [
        "YEAR", "MONTH", "MONTHNAME", "DAY", "WEEKDAY", "HOUR", "MINUTE", "SECOND",
        "PRESET", "COUNTER", "DPI", "COLOR",
    ];

    /// <param name="template">The template to expand.</param>
    /// <param name="timestamp">When the scan happened; date and time tokens read from it.</param>
    /// <param name="sessionId">Value for the legacy <c>{session}</c> placeholder.</param>
    /// <param name="facts">Values for the preset, counter, dpi and colour tokens.</param>
    /// <param name="extension">Appended when the expanded name does not already end with it; include the dot.</param>
    public static string Expand(
        string template,
        DateTimeOffset timestamp,
        string sessionId = "",
        TemplateFacts? facts = null,
        string extension = ".pdf")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        DateTimeOffset local = timestamp.ToLocalTime();
        TemplateFacts f = facts ?? new TemplateFacts();

        string expanded = Placeholder().Replace(template, m =>
        {
            string fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : "";
            string key = m.Groups["key"].Value;
            return key switch
            {
                "date" => local.ToString(fmt.Length == 0 ? "yyyy-MM-dd" : fmt, CultureInfo.InvariantCulture),
                "time" => local.ToString(fmt.Length == 0 ? "HHmm" : fmt, CultureInfo.InvariantCulture),
                "session" => sessionId,
                "YEAR" => local.ToString("yyyy", CultureInfo.InvariantCulture),
                "MONTH" => local.ToString("MM", CultureInfo.InvariantCulture),
                "MONTHNAME" => local.ToString("MMMM", CultureInfo.InvariantCulture),
                "DAY" => local.ToString("dd", CultureInfo.InvariantCulture),
                "WEEKDAY" => local.ToString("dddd", CultureInfo.InvariantCulture),
                "HOUR" => local.ToString("HH", CultureInfo.InvariantCulture),
                "MINUTE" => local.ToString("mm", CultureInfo.InvariantCulture),
                "SECOND" => local.ToString("ss", CultureInfo.InvariantCulture),
                "PRESET" => f.Preset,
                "COUNTER" => f.Counter.ToString(CultureInfo.InvariantCulture),
                "DPI" => f.Dpi.ToString(CultureInfo.InvariantCulture),
                "COLOR" => f.Color,
                // An unknown placeholder is left as written rather than silently dropped,
                // so a typo shows up in the name instead of vanishing.
                _ => m.Value,
            };
        });

        string name = Unsafe().Replace(expanded, "_").Trim().TrimStart('.');
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name += extension;
        }

        return name.Length <= extension.Length ? "scan" + extension : name;
    }

    /// <summary>Tokens that unknown placeholders in <paramref name="template"/> resolve to nothing.</summary>
    public static IReadOnlyList<string> UnknownTokens(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        return [.. Placeholder()
            .Matches(template)
            .Select(m => m.Groups["key"].Value)
            .Where(k => k is not ("date" or "time" or "session") && !Tokens.Contains(k, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)];
    }
}
