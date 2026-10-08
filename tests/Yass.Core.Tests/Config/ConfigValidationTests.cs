using Yass.Core.Config;
using Yass.Core.Domain;

namespace Yass.Core.Tests.Config;

public class ConfigValidationTests
{
    public static TheoryData<string, Func<YassConfig, YassConfig>> InvalidValues => new()
    {
        { "server.listen", c => c with { Server = c.Server with { Listen = "8080" } } },
        { "server.listen", c => c with { Server = c.Server with { Listen = "" } } },
        { "server.base_path", c => c with { Server = c.Server with { BasePath = "scan" } } },
        { "scanner.driver", c => c with { Scanner = c.Scanner with { Driver = (ScannerDriver)42 } } },
        { "scanner.poll_interval_seconds", c => c with { Scanner = c.Scanner with { PollIntervalSeconds = 0 } } },
        { "scanner.defaults.dpi", c => WithDefaults(c, d => d with { Dpi = 0 }) },
        { "scanner.defaults.dpi", c => WithDefaults(c, d => d with { Dpi = 5000 }) },
        { "scanner.defaults.color", c => WithDefaults(c, d => d with { Color = (ColorMode)9 }) },
        { "scanner.defaults.brightness", c => WithDefaults(c, d => d with { Brightness = 101 }) },
        { "scanner.defaults.contrast", c => WithDefaults(c, d => d with { Contrast = -101 }) },
        { "scanner.defaults.blank_page_threshold", c => WithDefaults(c, d => d with { BlankPageThreshold = 1.5 }) },
        { "scanner.ui_editable", c => c with { Scanner = c.Scanner with { UiEditable = ["dpi", "bogus"] } } },
        { "output.directory", c => c with { Output = c.Output with { Directory = "relative/consume" } } },
        { "output.pdf.jpeg_quality", c => c with { Output = c.Output with { Pdf = c.Output.Pdf with { JpegQuality = 101 } } } },
        { "output.pdf.compression", c => c with { Output = c.Output with { Pdf = c.Output.Pdf with { Compression = (PdfCompression)7 } } } },
        { "processing.max_concurrent_renders", c => c with { Processing = c.Processing with { MaxConcurrentRenders = 0 } } },
        { "processing.thumbnail_width", c => c with { Processing = c.Processing with { ThumbnailWidth = 10 } } },
        { "processing.preview_max_width", c => c with { Processing = c.Processing with { PreviewMaxWidth = 10000 } } },
        { "sessions.directory", c => c with { Sessions = c.Sessions with { Directory = "" } } },
        { "sessions.retention_hours", c => c with { Sessions = c.Sessions with { RetentionHours = -1 } } },
        { "logging.level", c => c with { Logging = c.Logging with { Level = (LogLevel)99 } } },
    };

    [Fact]
    public void Defaults_are_valid() => Assert.Empty(ConfigValidator.Validate(new YassConfig()));

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Invalid_value_is_reported_under_its_key(string expectedKey, Func<YassConfig, YassConfig> mutate)
    {
        YassConfig config = mutate(new YassConfig());

        IReadOnlyList<ConfigError> errors = ConfigValidator.Validate(config);

        ConfigError error = Assert.Single(errors);
        Assert.Equal(expectedKey, error.Key);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public void All_errors_are_collected()
    {
        YassConfig config = new YassConfig() with
        {
            Server = new ServerConfig { Listen = "nope" },
            Output = new OutputConfig { Pdf = new PdfConfig { JpegQuality = 0 } },
            Sessions = new SessionsConfig { Directory = "" },
        };

        IReadOnlyList<ConfigError> errors = ConfigValidator.Validate(config);

        Assert.Equal(["output.pdf.jpeg_quality", "server.listen", "sessions.directory"], errors.Select(e => e.Key).Order());
    }

    [Fact]
    public void Undefined_enum_message_lists_allowed_values()
    {
        YassConfig config = WithDefaults(new YassConfig(), d => d with { Color = (ColorMode)9 });

        ConfigError error = Assert.Single(ConfigValidator.Validate(config));

        Assert.Contains("bw, grayscale, color", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_ui_editable_key_message_names_the_key_and_the_allowed_set()
    {
        YassConfig config = new YassConfig() with { Scanner = new ScannerConfig { UiEditable = ["bogus"] } };

        ConfigError error = Assert.Single(ConfigValidator.Validate(config));

        Assert.Contains("bogus", error.Message, StringComparison.Ordinal);
        Assert.Contains("blank_page_detection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOrThrow_returns_config_when_valid()
    {
        var config = new YassConfig();

        Assert.Same(config, ConfigValidator.ValidateOrThrow(config));
    }

    [Fact]
    public void ValidateOrThrow_names_every_bad_key_in_the_message()
    {
        YassConfig config = WithDefaults(new YassConfig(), d => d with { Dpi = 1 }) with
        {
            Sessions = new SessionsConfig { RetentionHours = 99999 },
        };

        var ex = Assert.Throws<ConfigValidationException>(() => ConfigValidator.ValidateOrThrow(config));

        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains("scanner.defaults.dpi:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("sessions.retention_hours:", ex.Message, StringComparison.Ordinal);
    }

    private static YassConfig WithDefaults(YassConfig config, Func<ScanOptions, ScanOptions> mutate) =>
        config with { Scanner = config.Scanner with { Defaults = mutate(config.Scanner.Defaults) } };
}
