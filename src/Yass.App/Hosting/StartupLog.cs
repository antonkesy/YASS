namespace Yass.App.Hosting;

public static class StartupLog
{
    // missingConfig: a server is expected to have a config file; a desktop install runs on defaults.
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
