using System.Text;
using Yass.Core.Domain;

namespace Yass.Core.Config;

public static class ConfigKeys
{
    public static IReadOnlyList<string> ScanOptionKeys { get; } =
        typeof(ScanOptions).GetProperties().Select(p => ToSnakeCase(p.Name)).ToArray();

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
