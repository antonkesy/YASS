using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Core.Scanning;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ScannerDeviceTests
{
    [Fact]
    public async Task List_devices_reports_the_configured_one_first()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1) with
        {
            DeviceName = "YASS Fake ADF",
            ExtraDevices = ["Epson DS-530", "Canon LiDE 400"],
        });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        ScannerDeviceDto[] devices = await hub.ListDevices();

        Assert.Equal(["YASS Fake ADF", "Epson DS-530", "Canon LiDE 400"], devices.Select(d => d.Name));
        Assert.All(devices, d => Assert.Equal(d.Name, d.Id));
        Assert.All(devices, d => Assert.Equal("fake", d.Driver));
        Assert.All(devices, d => Assert.Null(d.Connection));
    }

    [Fact]
    public async Task Refresh_looks_again_instead_of_using_the_cache()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1) with { ExtraDevices = ["Epson DS-530"] });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        await hub.ListDevices();
        Assert.Equal(0, factory.Backend.Refreshes);

        ScannerDeviceDto[] devices = await hub.RefreshDevices();

        Assert.Equal(1, factory.Backend.Refreshes);
        Assert.Contains(devices, d => d.Name == "Epson DS-530");
        Assert.Equal(ScannerState.Ready, (await hub.GetScannerStatus()).State);
    }

    [Fact]
    public async Task No_device_means_nothing_to_choose_between()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1) with { NoDevice = true });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        Assert.Empty(await hub.ListDevices());
    }

    [Fact]
    public async Task A_scan_runs_on_the_device_it_asked_for()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1) with { ExtraDevices = ["Epson DS-530"] });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        // Run to the end so the factory is not torn down under a scan still writing pages.
        await hub.ScanToEnd(new ScanOptionsDto(Device: "Epson DS-530"));

        Assert.Equal("Epson DS-530", factory.Backend.LastOptions?.Device);
    }

    [Fact]
    public async Task A_scan_naming_a_device_that_is_not_there_fails_rather_than_using_another()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        // A bad device fails the session (like a jam) rather than the call; scanning the wrong machine is worse.
        SessionDto done = await hub.ScanToEnd(new ScanOptionsDto(Device: "Nothing Like This"));

        Assert.Equal(SessionState.Failed, done.State);
        Assert.Empty(done.Pages);
        Assert.Contains("Nothing Like This", done.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Device_is_honoured_whatever_the_deprecated_ui_editable_says()
    {
        // Legacy key: still accepted so old files load, but ignored.
        await using var factory = new YassFactory(
            FakeScenario.Pages(1) with { ExtraDevices = ["Epson DS-530"] },
            config => config with { Scanner = config.Scanner with { UiEditable = ["dpi"] } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        await hub.ScanToEnd(new ScanOptionsDto(Device: "Epson DS-530"));

        Assert.Equal("Epson DS-530", factory.Backend.LastOptions?.Device);
    }
}
