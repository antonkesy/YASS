using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Yass.Core.Config;

/// <summary>
/// Validates a <see cref="YassConfig"/> tree. Unlike <c>ValidateDataAnnotations()</c> this recurses into
/// nested sections, checks enums are defined, and reports every failure by its <c>yass.yaml</c> key.
/// </summary>
public static class ConfigValidator
{
    public static IReadOnlyList<ConfigError> Validate(YassConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = new List<ConfigError>();
        ValidateObject(config, prefix: "", errors);
        return errors;
    }

    /// <exception cref="ConfigValidationException">When any key is invalid.</exception>
    public static YassConfig ValidateOrThrow(YassConfig config)
    {
        IReadOnlyList<ConfigError> errors = Validate(config);
        return errors.Count == 0 ? config : throw new ConfigValidationException(errors);
    }

    private static void ValidateObject(object instance, string prefix, List<ConfigError> errors)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(instance);
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        foreach (ValidationResult result in results)
        {
            string member = result.MemberNames.FirstOrDefault() ?? "";
            string key = Join(prefix, member.Length == 0 ? "" : ConfigKeys.ToSnakeCase(member));
            errors.Add(new ConfigError(key, Describe(result)));
        }

        foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value = property.GetValue(instance);
            if (value is null)
            {
                continue;
            }

            string key = Join(prefix, ConfigKeys.ToSnakeCase(property.Name));
            Type type = property.PropertyType;

            if (type.IsEnum && !Enum.IsDefined(type, value))
            {
                errors.Add(new ConfigError(key, $"'{value}' is not one of {AllowedEnumValues(type)}"));
            }
            else if (IsSection(type))
            {
                ValidateObject(value, key, errors);
            }
        }
    }

    private static bool IsSection(Type type) =>
        type.IsClass && type != typeof(string) && type.Namespace?.StartsWith("Yass.", StringComparison.Ordinal) == true;

    private static string AllowedEnumValues(Type type) =>
        string.Join(", ", Enum.GetNames(type).Select(ConfigKeys.ToSnakeCase));

    private static string Describe(ValidationResult result) =>
        result.ErrorMessage is { Length: > 0 } message ? StripFieldPrefix(message) : "invalid value";

    /// <summary>Data annotations phrase messages as "The field Dpi must be between 50 and 1200."; the key already says which field.</summary>
    private static string StripFieldPrefix(string message)
    {
        const string prefix = "The field ";
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return message;
        }

        int space = message.IndexOf(' ', prefix.Length);
        return space < 0 ? message : message[(space + 1)..].TrimEnd('.');
    }

    private static string Join(string prefix, string key) =>
        prefix.Length == 0 ? key : key.Length == 0 ? prefix : prefix + "." + key;
}
