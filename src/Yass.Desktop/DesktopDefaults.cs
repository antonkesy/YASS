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
    /// The server's built-in <c>sessions.directory</c> (<c>/data/sessions</c>) names a container
    /// volume; on a desktop it means the per-user data folder. A path the file sets is kept.
    /// </summary>
    public static YassConfig Apply(YassConfig config, string sessionsDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Sessions.Directory == new SessionsConfig().Directory
            ? config with { Sessions = config.Sessions with { Directory = sessionsDirectory } }
            : config;
    }
}
