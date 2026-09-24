using Yawss.Core.Config;

namespace Yawss.Server.Config;

/// <summary>
/// Holds the live <see cref="YawssConfig"/>. <see cref="Reload"/> re-reads the file but keeps the original
/// <c>server</c> section, since the listener cannot be rebound at runtime.
/// </summary>
public sealed class ConfigStore(YawssConfig initial, string path, ILogger<ConfigStore> logger)
{
    private readonly object _gate = new();

    public string Path { get; } = path;

    public YawssConfig Current { get; private set; } = initial;

    /// <summary>Raised after a successful reload. Subscribers apply the non-<c>server</c> sections.</summary>
    public event EventHandler<ConfigChangedEventArgs>? Changed;

    /// <exception cref="ConfigValidationException">The file is invalid; the previous config stays active.</exception>
    public YawssConfig Reload()
    {
        YawssConfig fresh = ConfigLoader.Load(Path);
        YawssConfig next;
        lock (_gate)
        {
            next = fresh with { Server = Current.Server };
            Current = next;
        }

        logger.LogInformation("Configuration reloaded from {Path}", Path);
        Changed?.Invoke(this, new ConfigChangedEventArgs(next));
        return next;
    }
}

public sealed class ConfigChangedEventArgs(YawssConfig config) : EventArgs
{
    public YawssConfig Config { get; } = config;
}
