using NAPS2.Images;
using NAPS2.Scan;
using Yass.Core.Config;
using Yass.Core.Domain;
using Naps2PageSize = NAPS2.Images.PageSize;

namespace Yass.Scanning.Tests;

public class OptionMapperTests
{
    private static readonly ScanDevice Device = new(Driver.Sane, "epsonds:libusb:001:002", "DS-530II");

    [Theory]
    [InlineData(ScannerDriver.Auto, Driver.Default)]
    [InlineData(ScannerDriver.Sane, Driver.Sane)]
    [InlineData(ScannerDriver.Escl, Driver.Escl)]
    [InlineData(ScannerDriver.Wia, Driver.Wia)]
    public void Driver_maps(ScannerDriver ours, Driver naps2) => Assert.Equal(naps2, OptionMapper.MapDriver(ours, esclOnly: false));

    [Theory]
    [InlineData(ScannerDriver.Auto, Driver.Escl)]
    [InlineData(ScannerDriver.Sane, Driver.Sane)]
    [InlineData(ScannerDriver.Escl, Driver.Escl)]
    public void On_macOS_and_Android_auto_is_escl(ScannerDriver ours, Driver naps2) => Assert.Equal(naps2, OptionMapper.MapDriver(ours, esclOnly: true));

    [Theory]
    [InlineData(ScanSource.Flatbed, PaperSource.Flatbed)]
    [InlineData(ScanSource.Feeder, PaperSource.Feeder)]
    [InlineData(ScanSource.Duplex, PaperSource.Duplex)]
    public void Source_maps(ScanSource ours, PaperSource naps2) => Assert.Equal(naps2, OptionMapper.MapSource(ours));

    [Theory]
    [InlineData(ColorMode.Bw, BitDepth.BlackAndWhite)]
    [InlineData(ColorMode.Grayscale, BitDepth.Grayscale)]
    [InlineData(ColorMode.Color, BitDepth.Color)]
    public void Color_maps(ColorMode ours, BitDepth naps2) => Assert.Equal(naps2, OptionMapper.MapColor(ours));

    [Fact]
    public void Page_sizes_map_to_naps2_constants()
    {
        Assert.Same(Naps2PageSize.A4, OptionMapper.MapPageSize(Core.Domain.PageSize.A4));
        Assert.Same(Naps2PageSize.A5, OptionMapper.MapPageSize(Core.Domain.PageSize.A5));
        Assert.Same(Naps2PageSize.Letter, OptionMapper.MapPageSize(Core.Domain.PageSize.Letter));
        Assert.Same(Naps2PageSize.Legal, OptionMapper.MapPageSize(Core.Domain.PageSize.Legal));
    }

    [Fact]
    public void Full_option_set_maps_every_field()
    {
        var ours = new Core.Domain.ScanOptions
        {
            Source = ScanSource.Duplex,
            Dpi = 600,
            Color = ColorMode.Color,
            PageSize = Core.Domain.PageSize.Letter,
            Brightness = 20,
            Contrast = -30,
            BlankPageDetection = true,
            BlankPageThreshold = 0.05,
            AutoDeskew = true,
        };

        NAPS2.Scan.ScanOptions naps2 = OptionMapper.ToNaps2(ours, ScannerDriver.Escl, Device);

        Assert.Equal(Driver.Escl, naps2.Driver);
        Assert.Same(Device, naps2.Device);
        Assert.Equal(PaperSource.Duplex, naps2.PaperSource);
        Assert.Equal(600, naps2.Dpi);
        Assert.Equal(BitDepth.Color, naps2.BitDepth);
        Assert.Same(Naps2PageSize.Letter, naps2.PageSize);
        Assert.Equal(200, naps2.Brightness);
        Assert.Equal(-300, naps2.Contrast);
        Assert.True(naps2.ExcludeBlankPages);
        Assert.Equal(5, naps2.BlankPageCoverageThreshold);
        Assert.True(naps2.AutoDeskew);
        Assert.True(naps2.MaxQuality);
        Assert.Null(naps2.ThumbnailSize);
    }

    [Fact]
    public void Blank_detection_off_and_deskew_off_are_passed_through()
    {
        NAPS2.Scan.ScanOptions naps2 = OptionMapper.ToNaps2(new Core.Domain.ScanOptions { BlankPageDetection = false, AutoDeskew = false }, ScannerDriver.Auto, Device);

        Assert.False(naps2.ExcludeBlankPages);
        Assert.False(naps2.AutoDeskew);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.02, 2)]
    [InlineData(0.456, 46)]
    [InlineData(1.0, 100)]
    [InlineData(7.0, 100)]
    [InlineData(-1.0, 0)]
    public void Blank_threshold_scales_and_clamps(double ours, int naps2) => Assert.Equal(naps2, OptionMapper.MapBlankThreshold(ours));

    [Theory]
    [InlineData(100, 1000)]
    [InlineData(-100, -1000)]
    [InlineData(0, 0)]
    public void Brightness_scales_to_naps2_range(int ours, int naps2) =>
        Assert.Equal(naps2, OptionMapper.ToNaps2(new Core.Domain.ScanOptions { Brightness = ours }, ScannerDriver.Auto, Device).Brightness);

    [Fact]
    public void Device_selection_prefers_exact_id_then_substring_then_first()
    {
        var a = new ScanDevice(Driver.Sane, "epsonds:libusb:001:002", "DS-530II");
        var b = new ScanDevice(Driver.Sane, "pixma:04A91234", "Canon PIXMA");
        ScanDevice[] devices = [a, b];

        Assert.Same(a, OptionMapper.SelectDevice(devices, ""));
        Assert.Same(b, OptionMapper.SelectDevice(devices, "pixma:04A91234"));
        Assert.Same(b, OptionMapper.SelectDevice(devices, "canon"));
        Assert.Same(a, OptionMapper.SelectDevice(devices, "530"));
        Assert.Null(OptionMapper.SelectDevice(devices, "brother"));
        Assert.Null(OptionMapper.SelectDevice([], ""));
    }

    [Theory]
    [InlineData("v4l:/dev/video0")]
    [InlineData("gphoto2:usb:002,005")]
    [InlineData("GPHOTO2:usb:")]
    [InlineData("dc240:/dev/ttyS0")]
    [InlineData("qcam")]
    public void Sane_camera_backends_are_cameras(string id) =>
        Assert.True(OptionMapper.IsCamera(new ScanDevice(Driver.Sane, id, "cam")));

    [Theory]
    [InlineData(Driver.Sane, "epsonds:libusb:001:002")]
    [InlineData(Driver.Sane, "airscan:e0:Canon")]
    [InlineData(Driver.Sane, "v4lish:0")]
    [InlineData(Driver.Escl, "v4l:not-a-sane-id")]
    [InlineData(Driver.Wia, "{6BDD1FC6}\\0000")]
    public void Scanners_are_not_cameras(Driver driver, string id) =>
        Assert.False(OptionMapper.IsCamera(new ScanDevice(driver, id, "scanner")));

    [Fact]
    public void Cameras_are_left_out_of_the_device_list_in_order()
    {
        var scanner = new ScanDevice(Driver.Sane, "epsonds:libusb:001:002", "DS-530II");
        var webcam = new ScanDevice(Driver.Sane, "v4l:/dev/video0", "Integrated Camera");
        var other = new ScanDevice(Driver.Sane, "pixma:04A91234", "Canon PIXMA");

        Assert.Equal([scanner, other], OptionMapper.ScannersOnly([webcam, scanner, other]));
        Assert.Same(scanner, OptionMapper.SelectDevice(OptionMapper.ScannersOnly([webcam, scanner]), ""));
    }

    [Theory]
    [InlineData(0x0001_0000, false)] // scanner
    [InlineData(0x0001_0003, false)] // scanner, with a subtype in the low word
    [InlineData(0x0002_0000, true)] // digital camera
    [InlineData(0x0003_0001, true)] // streaming video (webcam)
    [InlineData(0, false)]
    public void Wia_device_types_name_cameras_by_their_high_word(int type, bool camera) =>
        Assert.Equal(camera, WiaDeviceKinds.IsCamera(type));

    [Fact]
    public void Wia_devices_are_cameras_when_wia_says_so()
    {
        var scanner = new ScanDevice(Driver.Wia, "{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}\\0000", "Epson DS-530");
        var webcam = new ScanDevice(Driver.Wia, "{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}\\0001", "Integrated Webcam");
        IReadOnlySet<string> cameras = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { webcam.ID.ToLowerInvariant() };

        Assert.True(OptionMapper.IsCamera(webcam, cameras));
        Assert.False(OptionMapper.IsCamera(scanner, cameras));
        Assert.False(OptionMapper.IsCamera(webcam));
        Assert.Equal([scanner], OptionMapper.ScannersOnly([webcam, scanner], cameras));
    }

    [Theory]
    [InlineData(ScannerDriver.Auto, false, false, "SANE")]
    [InlineData(ScannerDriver.Auto, true, false, "WIA")]
    [InlineData(ScannerDriver.Auto, false, true, "eSCL (network scanners)")]
    [InlineData(ScannerDriver.Escl, false, false, "eSCL (network scanners)")]
    [InlineData(ScannerDriver.Wia, false, false, "WIA")]
    [InlineData(ScannerDriver.Sane, true, false, "SANE")]
    public void Drivers_are_named_for_people_and_auto_for_what_it_resolves_to(ScannerDriver driver, bool windows, bool esclOnly, string label) =>
        Assert.Equal(label, OptionMapper.DriverLabel(driver, windows, esclOnly));
}
