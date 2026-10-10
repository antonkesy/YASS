using System.Globalization;
using System.Text.RegularExpressions;
using Yass.Core.Export;

namespace Yass.Export;

/// <summary>The legacy <c>{date:FORMAT}</c>, <c>{time:FORMAT}</c> and <c>{session}</c> tokens still expand so old names keep working.</summary>
public static partial class FilenameTemplate
{
    [GeneratedRegex(@"\{(?<key>[A-Za-z]+)(?::(?<fmt>[^}]*))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    public static readonly IReadOnlyList<string> Tokens =
    [
        "YEAR", "MONTH", "MONTHNAME", "DAY", "WEEKDAY", "HOUR", "MINUTE", "SECOND",
        "PRESET", "COUNTER", "DPI", "COLOR",
    ];

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
                // Unknown placeholders are kept so a typo shows up in the name instead of vanishing.
                _ => m.Value,
            };
        });

        string name = FileNames.Sanitize(expanded);
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name += extension;
        }

        return name.Length <= extension.Length ? "scan" + extension : name;
    }

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
