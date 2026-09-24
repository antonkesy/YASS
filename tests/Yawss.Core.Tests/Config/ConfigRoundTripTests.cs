using System.Collections;
using System.Reflection;
using Yawss.Core.Config;
using Yawss.Core.Domain;

namespace Yawss.Core.Tests.Config;

public class ConfigRoundTripTests
{
    /// <summary>A config where every key differs from its default, so a lost value cannot hide behind a default.</summary>
    private static readonly YawssConfig NonDefault = new()
    {
        Server = new ServerConfig { Listen = "127.0.0.1:9999", BasePath = "/scan" },
        Scanner = new ScannerConfig
        {
            Driver = ScannerDriver.Escl,
            Device = "Brother ADS",
            PollIntervalSeconds = 30,
            Defaults = new ScanOptions
            {
                Source = ScanSource.Duplex,
                Dpi = 600,
                Color = ColorMode.Color,
                PageSize = PageSize.Letter,
                Brightness = 10,
                Contrast = -10,
                BlankPageDetection = false,
                BlankPageThreshold = 0.1,
                AutoDeskew = true,
            },
            UiEditable = ["dpi", "auto_deskew"],
        },
        Output = new OutputConfig
        {
            Directory = "/mnt/consume",
            FilenameTemplate = "{date:yyyyMMdd}.pdf",
            Pdf = new PdfConfig { Compression = PdfCompression.Lossless, JpegQuality = 90 },
            AllowDownload = false,
            OnSuccess = OnSuccessAction.DeleteSession,
        },
        Processing = new ProcessingConfig { MaxConcurrentRenders = 4, ThumbnailWidth = 200, PreviewMaxWidth = 2000 },
        Sessions = new SessionsConfig { Directory = "/var/lib/yawss", RetentionHours = 48 },
        Logging = new LoggingConfig { Level = LogLevel.Debug, Format = LogFormat.Json },
    };

    [Fact]
    public void Defaults_survive_yaml_round_trip()
    {
        var original = new YawssConfig();

        YawssConfig restored = Yaml.Deserialize<YawssConfig>(Yaml.Serialize(original));

        AssertSameLeaves(original, restored);
    }

    [Fact]
    public void Non_default_values_survive_yaml_round_trip()
    {
        YawssConfig restored = Yaml.Deserialize<YawssConfig>(Yaml.Serialize(NonDefault));

        AssertSameLeaves(NonDefault, restored);
        Assert.Equal("127.0.0.1:9999", restored.Server.Listen);
        Assert.Equal(ScanSource.Duplex, restored.Scanner.Defaults.Source);
        Assert.Equal(["dpi", "auto_deskew"], restored.Scanner.UiEditable);
        Assert.Equal(OnSuccessAction.DeleteSession, restored.Output.OnSuccess);
    }

    [Fact]
    public void Every_leaf_key_appears_in_serialised_yaml()
    {
        string yaml = Yaml.Serialize(new YawssConfig());

        foreach (string key in Leaves(new YawssConfig()).Select(l => l.Key))
        {
            string leaf = key[(key.LastIndexOf('.') + 1)..];
            Assert.Contains(leaf + ":", yaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Enum_values_are_written_as_spec_spelling()
    {
        string yaml = Yaml.Serialize(new YawssConfig());

        Assert.Contains("driver: auto", yaml, StringComparison.Ordinal);
        Assert.Contains("source: feeder", yaml, StringComparison.Ordinal);
        Assert.Contains("color: grayscale", yaml, StringComparison.Ordinal);
        Assert.Contains("page_size: a4", yaml, StringComparison.Ordinal);
        Assert.Contains("compression: jpeg", yaml, StringComparison.Ordinal);
        Assert.Contains("on_success: keep_session", yaml, StringComparison.Ordinal);
        Assert.Contains("level: info", yaml, StringComparison.Ordinal);
        Assert.Contains("format: text", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Example_file_equals_defaults()
    {
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "yawss.example.yaml"));

        YawssConfig fromExample = Yaml.Deserialize<YawssConfig>(yaml);

        AssertSameLeaves(new YawssConfig(), fromExample);
    }

    [Fact]
    public void Example_file_is_valid()
    {
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "yawss.example.yaml"));

        YawssConfig fromExample = Yaml.Deserialize<YawssConfig>(yaml);

        Assert.Empty(ConfigValidator.Validate(fromExample));
    }

    [Fact]
    public void Example_file_mentions_every_key()
    {
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "yawss.example.yaml"));

        foreach (string key in Leaves(new YawssConfig()).Select(l => l.Key))
        {
            string leaf = key[(key.LastIndexOf('.') + 1)..];
            Assert.Contains(leaf + ":", yaml, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("PollIntervalSeconds", "poll_interval_seconds")]
    [InlineData("Dpi", "dpi")]
    [InlineData("UiEditable", "ui_editable")]
    [InlineData("BasePath", "base_path")]
    [InlineData("Pdf", "pdf")]
    public void Property_names_map_to_snake_case_keys(string property, string expected) =>
        Assert.Equal(expected, ConfigKeys.ToSnakeCase(property));

    [Fact]
    public void Scan_option_keys_match_spec() =>
        Assert.Equal(
            ["source", "dpi", "color", "page_size", "brightness", "contrast", "blank_page_detection", "blank_page_threshold", "auto_deskew"],
            ConfigKeys.ScanOptionKeys);

    /// <summary>Compares every leaf value by key. Records holding lists do not have value equality, so walk instead.</summary>
    private static void AssertSameLeaves(YawssConfig expected, YawssConfig actual)
    {
        var expectedLeaves = Leaves(expected).ToDictionary(l => l.Key, l => l.Value);
        var actualLeaves = Leaves(actual).ToDictionary(l => l.Key, l => l.Value);

        Assert.Equal(expectedLeaves.Keys.Order(), actualLeaves.Keys.Order());
        foreach ((string key, object? value) in expectedLeaves)
        {
            Assert.True(Equals(value, actualLeaves[key]), $"{key}: expected {value}, got {actualLeaves[key]}");
        }
    }

    private static IEnumerable<(string Key, object? Value)> Leaves(object instance, string prefix = "")
    {
        foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string key = prefix.Length == 0 ? ConfigKeys.ToSnakeCase(property.Name) : prefix + "." + ConfigKeys.ToSnakeCase(property.Name);
            object? value = property.GetValue(instance);

            if (value is string or null || value.GetType().IsValueType)
            {
                yield return (key, value);
            }
            else if (value is IEnumerable list)
            {
                yield return (key, string.Join(",", list.Cast<object>()));
            }
            else
            {
                foreach ((string Key, object? Value) leaf in Leaves(value, key))
                {
                    yield return leaf;
                }
            }
        }
    }
}
