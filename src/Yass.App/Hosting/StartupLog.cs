namespace Yass.App.Hosting;

/// <summary>What either host says about how it started: which config it read, and whether it is faking the hardware.</summary>
public static class StartupLog
{
    /// <param name="logger">The host's logger.</param>
    /// <param name="startup">The parsed command line.</param>
    /// <param name="missingConfig">
    /// How loudly to say there was no config file: a server is expected to have one, while a
    /// desktop install runs on its defaults until someone writes one.
    /// </param>
    public static void Write(ILogger logger, StartupOptions startup, LogLevel missingConfig)
    {
        ArgumentNullException.ThrowIfNull(startup);
        string path = Path.GetFullPath(startup.ConfigPath);
        if (File.Exists(path))
        {
            logger.LogInformation("Config loaded from {Path}", path);
        }
        else
        {
            logger.Log(missingConfig, "Config file {Path} not found; running with built-in defaults", path);
        }

        if (startup.Fake)
        {
            logger.LogInformation("Fake mode: scanner and PDF writer are simulated (scenario: {Scenario})", startup.FakeScenario ?? "default");
        }
    }
}
