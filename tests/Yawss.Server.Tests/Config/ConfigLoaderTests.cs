using System.Collections;
using Yawss.Core.Config;
using Yawss.Core.Domain;
using Yawss.Server.Config;
using Yawss.Server.Hosting;

namespace Yawss.Server.Tests.Config;

public class ConfigLoaderTests
{
    private static Hashtable Env(params (string Key, string Value)[] vars)
    {
        var table = new Hashtable();
        foreach ((string key, string value) in vars)
        {
            table[key] = value;
        }

        return table;
    }

    [Fact]
    public void Empty_file_yields_defaults() =>
        Assert.Equal(YawssYaml.Serialize(new YawssConfig()), YawssYaml.Serialize(ConfigLoader.Parse("", Env())));

    [Fact]
    public void Missing_file_yields_defaults() =>
        Assert.Equal(300, ConfigLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.yaml"), Env()).Scanner.Defaults.Dpi);

    [Fact]
    public void Yaml_keys_map_to_records()
    {
        YawssConfig config = ConfigLoader.Parse("""
            scanner:
              driver: escl
              defaults:
                color: bw
                page_size: letter
            output:
              on_success: delete_session
              pdf:
                compression: lossless
            """, Env());

        Assert.Equal(ScannerDriver.Escl, config.Scanner.Driver);
        Assert.Equal(ColorMode.Bw, config.Scanner.Defaults.Color);
        Assert.Equal(PageSize.Letter, config.Scanner.Defaults.PageSize);
        Assert.Equal(OnSuccessAction.DeleteSession, config.Output.OnSuccess);
        Assert.Equal(PdfCompression.Lossless, config.Output.Pdf.Compression);
    }

    [Fact]
    public void Environment_overrides_file_and_keeps_snake_case_keys()
    {
        YawssConfig config = ConfigLoader.Parse(
            "scanner:\n  poll_interval_seconds: 5\n  defaults:\n    dpi: 300\n",
            Env(("YAWSS__scanner__poll_interval_seconds", "9"), ("YAWSS__scanner__defaults__color", "color"), ("YAWSS__output__directory", "/mnt/x"), ("UNRELATED", "1")));

        Assert.Equal(9, config.Scanner.PollIntervalSeconds);
        Assert.Equal(300, config.Scanner.Defaults.Dpi);
        Assert.Equal(ColorMode.Color, config.Scanner.Defaults.Color);
        Assert.Equal("/mnt/x", config.Output.Directory);
    }

    [Fact]
    public void Invalid_value_names_the_key()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Parse("output:\n  pdf:\n    jpeg_quality: 500\n", Env()));

        Assert.Contains("output.pdf.jpeg_quality", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_key_is_rejected_by_name()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Parse("scanner:\n  dpi: 300\n", Env()));

        Assert.Contains("dpi", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_enum_value_is_rejected_by_name()
    {
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Parse("scanner:\n  defaults:\n    color: sepia\n", Env()));

        Assert.Contains("sepia", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Malformed_yaml_is_a_config_error() =>
        Assert.Throws<ConfigValidationException>(() => ConfigLoader.Parse("scanner: [", Env()));

    [Theory]
    [InlineData("", "yawss.yaml", false, null)]
    [InlineData("--config /etc/yawss.yaml", "/etc/yawss.yaml", false, null)]
    [InlineData("--fake", "yawss.yaml", true, null)]
    [InlineData("--fake pages=2,error=1 --config x.yaml", "x.yaml", true, "pages=2,error=1")]
    [InlineData("--urls http://x --fake", "yawss.yaml", true, null)]
    public void Startup_options_parse_command_line(string commandLine, string config, bool fake, string? scenario)
    {
        StartupOptions options = StartupOptions.Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(config, options.ConfigPath);
        Assert.Equal(fake, options.Fake);
        Assert.Equal(scenario, options.FakeScenario);
    }
}
