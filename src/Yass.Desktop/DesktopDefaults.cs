using Yass.Core.Config;

namespace Yass.Desktop;

/// <summary>Where a desktop install keeps its files, and the config defaults that follow from that.</summary>
public static class DesktopDefaults
{
    /// <summary><c>yass/yass.yaml</c> in the per-user config folder (XDG config, <c>%APPDATA%</c>, Application Support).</summary>
    public static string ConfigPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "yass", "yass.yaml");

    /// <summary><c>yass/sessions</c> in the per-user data folder (XDG data, <c>%LOCALAPPDATA%</c>, Application Support).</summary>
    public static string SessionsDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "yass", "sessions");

    /// <summary>
    /// Where the server's built-in values assume a container or a small home server, the desktop's
    /// own; a value the file sets is kept.
    /// <list type="bullet">
    /// <item><c>sessions.directory</c> (<c>/data/sessions</c>, a container volume) becomes the per-user data folder.</item>
    /// <item><c>processing.max_concurrent_renders</c> (2, for a small box) becomes half the cores, up to
    /// <see cref="MaxRenders"/>: each render holds a full-size bitmap, and the memory budget is the server's.</item>
    /// </list>
    /// </summary>
    public static YassConfig Apply(YassConfig config, string sessionsDirectory, int processorCount)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Sessions.Directory == new SessionsConfig().Directory)
        {
            config = config with { Sessions = config.Sessions with { Directory = sessionsDirectory } };
        }

        if (config.Processing.MaxConcurrentRenders == new ProcessingConfig().MaxConcurrentRenders)
        {
            int renders = Math.Clamp(processorCount / 2, config.Processing.MaxConcurrentRenders, MaxRenders);
            config = config with { Processing = config.Processing with { MaxConcurrentRenders = renders } };
        }

        return config;
    }

    /// <summary>Four 300 dpi colour A4 bitmaps are about 140 MB, well inside the 400 MB scan budget (SPEC §9).</summary>
    public const int MaxRenders = 4;
}
