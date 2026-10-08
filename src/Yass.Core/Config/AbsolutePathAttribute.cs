using System.ComponentModel.DataAnnotations;

namespace Yass.Core.Config;

/// <summary>Empty, or an absolute path: a relative one would land wherever the service was started from.</summary>
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
