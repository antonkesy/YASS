using System.Text;
using Yawss.Core.Domain;

namespace Yawss.Core.Config;

/// <summary>Naming helpers between C# properties and <c>yawss.yaml</c> keys.</summary>
public static class ConfigKeys
{
    /// <summary>Snake_case names of every <see cref="ScanOptions"/> property, i.e. the values allowed in <c>scanner.ui_editable</c>.</summary>
    public static IReadOnlyList<string> ScanOptionKeys { get; } =
        typeof(ScanOptions).GetProperties().Select(p => ToSnakeCase(p.Name)).ToArray();

    /// <summary><c>PollIntervalSeconds</c> → <c>poll_interval_seconds</c>.</summary>
    public static string ToSnakeCase(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        var sb = new StringBuilder(propertyName.Length + 4);
        for (int i = 0; i < propertyName.Length; i++)
        {
            char c = propertyName[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
