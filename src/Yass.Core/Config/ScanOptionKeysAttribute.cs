using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ScanOptionKeysAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable<string> keys)
        {
            return ValidationResult.Success;
        }

        string[] unknown = keys.Where(k => !ConfigKeys.ScanOptionKeys.Contains(k, StringComparer.Ordinal)).ToArray();
        if (unknown.Length == 0)
        {
            return ValidationResult.Success;
        }

        string allowed = string.Join(", ", ConfigKeys.ScanOptionKeys);
        return new ValidationResult(
            $"unknown key(s) {string.Join(", ", unknown)}; allowed: {allowed}",
            validationContext.MemberName is { } name ? [name] : null);
    }
}
