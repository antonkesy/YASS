namespace Yass.App.Hosting;

/// <summary>Command line of either host. Everything else comes from <c>yass.yaml</c>.</summary>
/// <param name="ConfigPath">Path of the YAML file; missing file = defaults.</param>
/// <param name="Fake">Use the fake scanner and document writer instead of NAPS2.</param>
/// <param name="FakeScenario">Optional <c>key=value</c> scenario string for the fake backend.</param>
public sealed record StartupOptions(string ConfigPath, bool Fake, string? FakeScenario)
{
    public const string ConfigEnv = "YASS_CONFIG";
    public const string FakeEnv = "YASS_FAKE";
    public const string FakeScenarioEnv = "YASS_FAKE_SCENARIO";

    /// <summary>
    /// <c>--config PATH</c>, <c>--fake [SCENARIO]</c>. Environment fallbacks: <c>YASS_CONFIG</c>, <c>YASS_FAKE=1</c>, <c>YASS_FAKE_SCENARIO</c>.
    /// Unknown arguments are left for the host. Without either, the config is <paramref name="defaultConfigPath"/>.
    /// </summary>
    public static StartupOptions Parse(IReadOnlyList<string> args, string defaultConfigPath = "yass.yaml")
    {
        ArgumentNullException.ThrowIfNull(args);
        string configPath = Environment.GetEnvironmentVariable(ConfigEnv) ?? defaultConfigPath;
        bool fake = Environment.GetEnvironmentVariable(FakeEnv) is "1" or "true";
        string? scenario = Environment.GetEnvironmentVariable(FakeScenarioEnv);

        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Count:
                    configPath = args[++i];
                    break;
                case "--fake":
                    fake = true;
                    if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        scenario = args[++i];
                    }

                    break;
                default:
                    break;
            }
        }

        return new StartupOptions(configPath, fake, scenario);
    }
}
