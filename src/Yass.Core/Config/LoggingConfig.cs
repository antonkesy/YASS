namespace Yass.Core.Config;

public enum LogLevel
{
    Trace = 0,
    Debug,
    Info,
    Warning,
    Error,
    Critical,
}

public enum LogFormat
{
    Text = 0,
    Json,
}

public sealed record LoggingConfig
{
    public LogLevel Level { get; init; } = LogLevel.Info;

    public LogFormat Format { get; init; } = LogFormat.Text;
}
