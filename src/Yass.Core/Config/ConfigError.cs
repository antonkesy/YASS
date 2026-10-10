namespace Yass.Core.Config;

public sealed record ConfigError(string Key, string Message)
{
    public override string ToString() => $"{Key}: {Message}";
}
