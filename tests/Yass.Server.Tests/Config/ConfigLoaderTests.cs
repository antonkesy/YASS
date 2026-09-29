using System.Collections;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Server.Config;
using Yass.Server.Hosting;

namespace Yass.Server.Tests.Config;

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
        Assert.Equal(YassYaml.Serialize(new YassConfig()), YassYaml.Serialize(ConfigLoader.Parse("", Env())));

    [Fact]
    public void Missing_file_yields_defaults() =>
        Assert.Equal(300, ConfigLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.yaml"), Env()).Scanner.Defaults.Dpi);

    /// <summary>
    /// Reading a page is local and happens only when Find name asks for it, so it is on out of
    /// the box; asking a model is what stays off, because that is the part that can leave.
    /// </summary>
    [Fact]
    public void Reading_pages_is_on_by_default_and_asking_a_model_is_not()
    {
        NamingConfig naming = ConfigLoader.Parse("", Env()).Naming;

        Assert.True(naming.Ocr);
        Assert.Equal(NamingProvider.Off, naming.Provider);
    }

    [Fact]
    public void Yaml_keys_map_to_records()
    {
        YassConfig config = ConfigLoader.Parse("""
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
        YassConfig config = ConfigLoader.Parse(
            "scanner:\n  poll_interval_seconds: 5\n  defaults:\n    dpi: 300\n",
            Env(("YASS__scanner__poll_interval_seconds", "9"), ("YASS__scanner__defaults__color", "color"), ("YASS__output__directory", "/mnt/x"), ("UNRELATED", "1")));

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
    [InlineData("", "yass.yaml", false, null)]
    [InlineData("--config /etc/yass.yaml", "/etc/yass.yaml", false, null)]
    [InlineData("--fake", "yass.yaml", true, null)]
    [InlineData("--fake pages=2,error=1 --config x.yaml", "x.yaml", true, "pages=2,error=1")]
    [InlineData("--urls http://x --fake", "yass.yaml", true, null)]
    public void Startup_options_parse_command_line(string commandLine, string config, bool fake, string? scenario)
    {
        StartupOptions options = StartupOptions.Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(config, options.ConfigPath);
        Assert.Equal(fake, options.Fake);
        Assert.Equal(scenario, options.FakeScenario);
    }
}
