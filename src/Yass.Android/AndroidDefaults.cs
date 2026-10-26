using Yass.Core.Config;

namespace Yass.Android;

/// <summary>Everything lives in the app's private storage; the config file may be absent.</summary>
public static class AndroidDefaults
{
    public static string ConfigPath(string filesDirectory) => Path.Combine(filesDirectory, "yass.yaml");

    public static string SessionsDirectory(string filesDirectory) => Path.Combine(filesDirectory, "sessions");

    /// <summary>Replaces the server's container path for sessions; a value the file sets is kept.</summary>
    public static YassConfig Apply(YassConfig config, string sessionsDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Sessions.Directory == new SessionsConfig().Directory
            ? config with { Sessions = config.Sessions with { Directory = sessionsDirectory } }
            : config;
    }
}
