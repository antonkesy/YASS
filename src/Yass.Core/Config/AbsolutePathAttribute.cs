using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AbsolutePathAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        if (value is not string path || path.Length == 0 || Path.IsPathFullyQualified(path))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            $"'{path}' must be an absolute path, or empty to turn it off",
            validationContext.MemberName is { } name ? [name] : null);
    }
}
