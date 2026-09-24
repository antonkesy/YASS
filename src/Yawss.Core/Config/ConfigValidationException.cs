namespace Yawss.Core.Config;

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException()
        : this("Invalid configuration.")
    {
    }

    public ConfigValidationException(string message)
        : base(message)
    {
    }

    public ConfigValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConfigValidationException(IReadOnlyList<ConfigError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<ConfigError> Errors { get; } = [];

    private static string BuildMessage(IReadOnlyList<ConfigError> errors) =>
        "Invalid configuration:" + string.Concat(errors.Select(e => Environment.NewLine + "  " + e));
}
