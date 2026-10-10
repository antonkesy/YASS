using Yass.App.Config;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.App.Scanning;

public sealed class EffectiveOptions(ConfigStore config)
{
    public ScanOptionsDto Current() => ToDto(config.Current.Scanner.Defaults);

    /// <summary>Held to the same bounds as <c>scanner.defaults</c> in yass.yaml.</summary>
    public ScanOptions Resolve(ScanOptionsDto? requested)
    {
        ScanOptions d = config.Current.Scanner.Defaults;
        if (requested is not { } r)
        {
            return d;
        }

        ScanOptions options = d with
        {
            Source = r.Source ?? d.Source,
            Dpi = r.Dpi ?? d.Dpi,
            Color = r.Color ?? d.Color,
            PageSize = r.PageSize ?? d.PageSize,
            Brightness = r.Brightness ?? d.Brightness,
            Contrast = r.Contrast ?? d.Contrast,
            BlankPageDetection = r.BlankPageDetection ?? d.BlankPageDetection,
            BlankPageThreshold = r.BlankPageThreshold ?? d.BlankPageThreshold,
            AutoDeskew = r.AutoDeskew ?? d.AutoDeskew,
            Device = r.Device ?? d.Device,
        };

        IReadOnlyList<ConfigError> errors = ConfigValidator.ValidateSection(options);
        return errors.Count == 0
            ? options
            : throw new ArgumentException("Invalid scan options: " + string.Join("; ", errors), nameof(requested));
    }

    public static ScanOptionsDto ToDto(ScanOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ScanOptionsDto(
            options.Source,
            options.Dpi,
            options.Color,
            options.PageSize,
            options.Brightness,
            options.Contrast,
            options.BlankPageDetection,
            options.BlankPageThreshold,
            options.AutoDeskew)
        {
            Device = options.Device,
        };
    }
}
