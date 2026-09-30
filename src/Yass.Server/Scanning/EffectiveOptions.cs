using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Export;
using Yass.Core.Hub;
using Yass.Server.Config;

namespace Yass.Server.Scanning;

/// <summary>Merges <c>scanner.defaults</c> with client overrides, honouring only <c>scanner.ui_editable</c> keys.</summary>
public sealed class EffectiveOptions(ConfigStore config)
{
    /// <summary>Defaults plus the editable key list, for the settings UI.</summary>
    public ScanOptionsDto Current()
    {
        ScannerConfig scanner = config.Current.Scanner;
        OutputConfig output = config.Current.Output;
        return ToDto(scanner.Defaults, scanner.UiEditable) with
        {
            AllowDownload = output.AllowDownload,
            ExportRoots = [.. ExportRoots.Effective(output.AllowedRoots, output.Directory)],
        };
    }

    /// <summary>Applies <paramref name="requested"/> on top of the defaults; non-editable keys are ignored.</summary>
    public ScanOptions Resolve(ScanOptionsDto? requested)
    {
        ScannerConfig scanner = config.Current.Scanner;
        ScanOptions options = scanner.Defaults;
        if (requested is null)
        {
            return options;
        }

        var editable = new HashSet<string>(scanner.UiEditable, StringComparer.Ordinal);

        if (editable.Contains("source") && requested.Source is { } source)
        {
            options = options with { Source = source };
        }

        if (editable.Contains("dpi") && requested.Dpi is { } dpi)
        {
            options = options with { Dpi = dpi };
        }

        if (editable.Contains("color") && requested.Color is { } color)
        {
            options = options with { Color = color };
        }

        if (editable.Contains("page_size") && requested.PageSize is { } pageSize)
        {
            options = options with { PageSize = pageSize };
        }

        if (editable.Contains("brightness") && requested.Brightness is { } brightness)
        {
            options = options with { Brightness = brightness };
        }

        if (editable.Contains("contrast") && requested.Contrast is { } contrast)
        {
            options = options with { Contrast = contrast };
        }

        if (editable.Contains("blank_page_detection") && requested.BlankPageDetection is { } blank)
        {
            options = options with { BlankPageDetection = blank };
        }

        if (editable.Contains("blank_page_threshold") && requested.BlankPageThreshold is { } threshold)
        {
            options = options with { BlankPageThreshold = threshold };
        }

        if (editable.Contains("auto_deskew") && requested.AutoDeskew is { } deskew)
        {
            options = options with { AutoDeskew = deskew };
        }

        if (editable.Contains("device") && requested.Device is { } device)
        {
            options = options with { Device = device };
        }

        return options;
    }

    public static ScanOptionsDto ToDto(ScanOptions options, string[]? editable = null)
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
            options.AutoDeskew,
            editable)
        {
            Device = options.Device,
        };
    }
}
