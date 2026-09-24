namespace Yawss.Core.Config;

/// <summary>One invalid value, addressed by its dotted snake_case key such as <c>scanner.defaults.dpi</c>.</summary>
public sealed record ConfigError(string Key, string Message)
{
    public override string ToString() => $"{Key}: {Message}";
}
