using Yass.Core.Config;

namespace Yass.App.Config;

/// <summary>
/// Holds the live <see cref="YassConfig"/>. <see cref="Reload"/> re-reads the file but keeps the original
/// <c>server</c> section, since the listener cannot be rebound at runtime. The host's own defaults
/// (<paramref name="hostDefaults"/>) are applied to every reload as they were to the first load.
/// </summary>
public sealed class ConfigStore(YassConfig initial, string path, ILogger<ConfigStore> logger, Func<YassConfig, YassConfig>? hostDefaults = null)
{
    private readonly Lock _gate = new();

    public string Path { get; } = path;

    public YassConfig Current { get; private set; } = initial;

    /// <exception cref="ConfigValidationException">The file is invalid; the previous config stays active.</exception>
    public YassConfig Reload()
    {
        YassConfig fresh = ConfigLoader.Load(Path);
        fresh = hostDefaults?.Invoke(fresh) ?? fresh;
        YassConfig previous;
        YassConfig next;
        lock (_gate)
        {
            previous = Current;
            next = fresh with { Server = previous.Server };
            Current = next;
        }

        logger.LogInformation("Configuration reloaded from {Path}", Path);
        if (next.Sessions.Directory != previous.Sessions.Directory)
        {
            logger.LogWarning("sessions.directory changed to {Directory}; it takes effect after a restart", next.Sessions.Directory);
        }

        return next;
    }
}
