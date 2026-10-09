using NAPS2.Images;
using NAPS2.Scan;
using Yass.Core.Config;
using Yass.Core.Domain;
using Naps2Options = NAPS2.Scan.ScanOptions;
using Naps2PageSize = NAPS2.Images.PageSize;

namespace Yass.Scanning;

/// <summary>Pure mapping between YASS options and NAPS2's <see cref="Naps2Options"/>. Kept free of I/O for unit tests.</summary>
public static class OptionMapper
{
    /// <summary>NAPS2 brightness/contrast run -1000..1000; ours -100..100.</summary>
    public const int BrightnessContrastScale = 10;

    public static Driver MapDriver(ScannerDriver driver) => MapDriver(driver, OperatingSystem.IsMacOS());

    /// <param name="driver"></param>
    /// <param name="macOS">
    /// NAPS2's default there is its Apple driver, which only exists in a <c>-macos</c> build of the SDK;
    /// eSCL is what a plain .NET build can scan with.
    /// </param>
    public static Driver MapDriver(ScannerDriver driver, bool macOS) => driver switch
    {
        ScannerDriver.Sane => Driver.Sane,
        ScannerDriver.Escl => Driver.Escl,
        ScannerDriver.Wia => Driver.Wia,
        _ when macOS => Driver.Escl,
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

    /// <summary>
    /// NAPS2 expresses the blank-page coverage threshold as 0..100 (mapped internally onto 0..1 % ink coverage);
    /// our <c>blank_page_threshold</c> is a 0..1 fraction. Scale and clamp.
    /// </summary>
    public static int MapBlankThreshold(double threshold) => (int)Math.Clamp(Math.Round(threshold * 100), 0, 100);

    /// <summary>Builds NAPS2 options for one scan run. Blank pages are excluded and deskew applied by NAPS2 itself (ADR-004).</summary>
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

    /// <summary>
    /// Picks the configured device: exact id, then case-insensitive substring of id or name; empty = first.
    /// Returns <c>null</c> when nothing matches.
    /// </summary>
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

    /// <summary>
    /// SANE backends that drive cameras rather than scanners: webcams (<c>v4l</c>), digital
    /// cameras (<c>gphoto2</c> and the old per-model ones) and a microscope camera. A camera is
    /// not a scan source here, so their devices are never offered.
    /// </summary>
    public static readonly IReadOnlySet<string> CameraBackends =
        new HashSet<string>(["v4l", "gphoto2", "dc210", "dc240", "dc25", "dmc", "qcam", "stv680"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a device is a camera. A SANE id is <c>backend:address</c>, so the backend says; a
    /// WIA id says nothing, so <paramref name="wiaCameras"/> lists the ones WIA calls cameras.
    /// </summary>
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

    /// <summary>The devices that can scan, cameras left out.</summary>
    public static IReadOnlyList<ScanDevice> ScannersOnly(IEnumerable<ScanDevice> devices, IReadOnlySet<string>? wiaCameras = null) =>
        [.. devices.Where(d => !IsCamera(d, wiaCameras))];

    /// <summary>The driver a search ran with, named for a person: what <c>auto</c> resolves to on this machine.</summary>
    public static string DriverLabel(ScannerDriver driver) => DriverLabel(driver, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    public static string DriverLabel(ScannerDriver driver, bool windows, bool macOS) => driver switch
    {
        ScannerDriver.Sane => "SANE",
        ScannerDriver.Escl => "eSCL (network scanners)",
        ScannerDriver.Wia => "WIA",
        _ when windows => "WIA",
        _ when macOS => "eSCL (network scanners)",
        _ => "SANE",
    };
}
