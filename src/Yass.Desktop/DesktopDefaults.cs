using Yass.Core.Config;

namespace Yass.Desktop;

public static class DesktopDefaults
{
    public static string ConfigPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "yass", "yass.yaml");

    public static string DataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "yass");

    public static string SessionsDirectory() => Path.Combine(DataDirectory(), "sessions");

    public static string WebViewDataDirectory() => Path.Combine(DataDirectory(), "webview");

    /// <summary>Replaces server defaults that assume a container or small box; values the file sets are kept.</summary>
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
