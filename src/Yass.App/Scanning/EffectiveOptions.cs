using Yass.App.Config;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.App.Scanning;

/// <summary>Merges <c>scanner.defaults</c> with client overrides. Every key the client sends is honoured: yass.yaml only supplies defaults.</summary>
public sealed class EffectiveOptions(ConfigStore config)
{
    /// <summary>The defaults, for the settings UI.</summary>
    public ScanOptionsDto Current()
    {
        return ToDto(config.Current.Scanner.Defaults);
    }

    /// <summary>Applies <paramref name="requested"/> on top of the defaults.</summary>
    public ScanOptions Resolve(ScanOptionsDto? requested)
    {
        ScannerConfig scanner = config.Current.Scanner;
        ScanOptions options = scanner.Defaults;
        if (requested is null)
        {
            return options;
        }

        if (requested.Source is { } source)
        {
            options = options with { Source = source };
        }

        if (requested.Dpi is { } dpi)
        {
            options = options with { Dpi = dpi };
        }

        if (requested.Color is { } color)
        {
            options = options with { Color = color };
        }

        if (requested.PageSize is { } pageSize)
        {
            options = options with { PageSize = pageSize };
        }

        if (requested.Brightness is { } brightness)
        {
            options = options with { Brightness = brightness };
        }

        if (requested.Contrast is { } contrast)
        {
            options = options with { Contrast = contrast };
        }

        if (requested.BlankPageDetection is { } blank)
        {
            options = options with { BlankPageDetection = blank };
        }

        if (requested.BlankPageThreshold is { } threshold)
        {
            options = options with { BlankPageThreshold = threshold };
        }

        if (requested.AutoDeskew is { } deskew)
        {
            options = options with { AutoDeskew = deskew };
        }

        if (requested.Device is { } device)
        {
            options = options with { Device = device };
        }

        return options;
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
