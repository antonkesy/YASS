using NAPS2.Images;
using NAPS2.Scan;
using Yass.Core.Config;
using Yass.Core.Domain;
using Naps2Options = NAPS2.Scan.ScanOptions;
using Naps2PageSize = NAPS2.Images.PageSize;

namespace Yass.Scanning;

public static class OptionMapper
{
    /// <summary>NAPS2 brightness/contrast run -1000..1000; ours -100..100.</summary>
    public const int BrightnessContrastScale = 10;

    /// <summary>
    /// On macOS NAPS2's default Apple driver needs a -macos SDK build, and Android has no scanner stack at all:
    /// plain .NET can only scan via eSCL there.
    /// </summary>
    public static bool EsclOnly => OperatingSystem.IsMacOS() || OperatingSystem.IsAndroid();

    public static Driver MapDriver(ScannerDriver driver) => MapDriver(driver, EsclOnly);

    public static Driver MapDriver(ScannerDriver driver, bool esclOnly) => driver switch
    {
        ScannerDriver.Sane => Driver.Sane,
        ScannerDriver.Escl => Driver.Escl,
        ScannerDriver.Wia => Driver.Wia,
        _ when esclOnly => Driver.Escl,
        _ => Driver.Default,
    };

    public static PaperSource MapSource(ScanSource source) => source switch
    {
        ScanSource.Flatbed => PaperSource.Flatbed,
        ScanSource.Duplex => PaperSource.Duplex,
        _ => PaperSource.Feeder,
    };

    public static BitDepth MapColor(ColorMode color) => color switch
    {
        ColorMode.Bw => BitDepth.BlackAndWhite,
        ColorMode.Color => BitDepth.Color,
        _ => BitDepth.Grayscale,
    };

    public static Naps2PageSize MapPageSize(Core.Domain.PageSize size) => size switch
    {
        Core.Domain.PageSize.A5 => Naps2PageSize.A5,
        Core.Domain.PageSize.Letter => Naps2PageSize.Letter,
        Core.Domain.PageSize.Legal => Naps2PageSize.Legal,
        _ => Naps2PageSize.A4,
    };

    // NAPS2's blank threshold is 0..100; ours is a 0..1 fraction.
    public static int MapBlankThreshold(double threshold) => (int)Math.Clamp(Math.Round(threshold * 100), 0, 100);

    /// <summary>Blank-page exclusion and deskew are done by NAPS2 itself (SPEC.md §6).</summary>
    public static Naps2Options ToNaps2(Core.Domain.ScanOptions options, ScannerDriver driver, ScanDevice device)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(device);

        return new Naps2Options
        {
            Driver = MapDriver(driver),
            Device = device,
            PaperSource = MapSource(options.Source),
            Dpi = options.Dpi,
            BitDepth = MapColor(options.Color),
            PageSize = MapPageSize(options.PageSize),
            Brightness = Math.Clamp(options.Brightness * BrightnessContrastScale, -1000, 1000),
            Contrast = Math.Clamp(options.Contrast * BrightnessContrastScale, -1000, 1000),
            BrightnessContrastAfterScan = true,
            MaxQuality = true,
            ThumbnailSize = null,
            ExcludeBlankPages = options.BlankPageDetection,
            BlankPageCoverageThreshold = MapBlankThreshold(options.BlankPageThreshold),
            AutoDeskew = options.AutoDeskew,
        };
    }

    /// <summary>Exact id, then case-insensitive substring of id or name; empty = first. Null when nothing matches.</summary>
    public static ScanDevice? SelectDevice(IReadOnlyList<ScanDevice> devices, string configured)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            return devices[0];
        }

        return devices.FirstOrDefault(d => string.Equals(d.ID, configured, StringComparison.Ordinal))
            ?? devices.FirstOrDefault(d =>
                d.ID.Contains(configured, StringComparison.OrdinalIgnoreCase) ||
                d.Name.Contains(configured, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>SANE backends that drive cameras (v4l, gphoto2, ...), never offered as scan sources.</summary>
    public static readonly IReadOnlySet<string> CameraBackends =
        new HashSet<string>(["v4l", "gphoto2", "dc210", "dc240", "dc25", "dmc", "qcam", "stv680"], StringComparer.OrdinalIgnoreCase);

    // A SANE id is backend:address so the backend says; a WIA id says nothing, hence wiaCameras.
    public static bool IsCamera(ScanDevice device, IReadOnlySet<string>? wiaCameras = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        switch (device.Driver)
        {
            case Driver.Sane:
                int colon = device.ID.IndexOf(':', StringComparison.Ordinal);
                return CameraBackends.Contains(colon < 0 ? device.ID : device.ID[..colon]);
            case Driver.Wia:
                return wiaCameras?.Contains(device.ID) == true;
            default:
                return false;
        }
    }

    public static IReadOnlyList<ScanDevice> ScannersOnly(IEnumerable<ScanDevice> devices, IReadOnlySet<string>? wiaCameras = null) =>
        [.. devices.Where(d => !IsCamera(d, wiaCameras))];

    public static string DriverLabel(ScannerDriver driver) => DriverLabel(driver, OperatingSystem.IsWindows(), EsclOnly);

    public static string DriverLabel(ScannerDriver driver, bool windows, bool esclOnly) => driver switch
    {
        ScannerDriver.Sane => "SANE",
        ScannerDriver.Escl => "eSCL (network scanners)",
        ScannerDriver.Wia => "WIA",
        _ when windows => "WIA",
        _ when esclOnly => "eSCL (network scanners)",
        _ => "SANE",
    };
}
