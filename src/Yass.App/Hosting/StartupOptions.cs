namespace Yass.App.Hosting;

public sealed record StartupOptions(string ConfigPath, bool Fake, string? FakeScenario)
{
    public const string ConfigEnv = "YASS_CONFIG";
    public const string FakeEnv = "YASS_FAKE";
    public const string FakeScenarioEnv = "YASS_FAKE_SCENARIO";

    /// <summary>Environment fallbacks: <c>YASS_CONFIG</c>, <c>YASS_FAKE=1</c>, <c>YASS_FAKE_SCENARIO</c>. Unknown arguments are left for the host.</summary>
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
