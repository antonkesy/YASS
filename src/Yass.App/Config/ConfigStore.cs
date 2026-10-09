using Yass.Core.Config;

namespace Yass.App.Config;

/// <summary>
/// Holds the live <see cref="YassConfig"/>. <see cref="Reload"/> re-reads the file but keeps the original
/// <c>server</c> section, since the listener cannot be rebound at runtime. The host's own defaults
/// (<paramref name="hostDefaults"/>) are applied to every reload as they were to the first load.
/// </summary>
public sealed class ConfigStore(YassConfig initial, string path, ILogger<ConfigStore> logger, Func<YassConfig, YassConfig>? hostDefaults = null)
{
    private readonly object _gate = new();

    public string Path { get; } = path;

    public YassConfig Current { get; private set; } = initial;

    /// <summary>Raised after a successful reload. Subscribers apply the non-<c>server</c> sections.</summary>
    public event EventHandler<ConfigChangedEventArgs>? Changed;

    /// <exception cref="ConfigValidationException">The file is invalid; the previous config stays active.</exception>
    public YassConfig Reload()
    {
        YassConfig fresh = ConfigLoader.Load(Path);
        fresh = hostDefaults?.Invoke(fresh) ?? fresh;
        YassConfig next;
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

public sealed class ConfigChangedEventArgs(YassConfig config) : EventArgs
{
    public YassConfig Config { get; } = config;
}
