using Yass.Core.Domain;
using Yass.Core.Hub;
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
        // The fake has no driver identifier to be distinct from what it is called.
        Assert.All(devices, d => Assert.Equal(d.Name, d.Id));
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

        await hub.StartScan(new ScanOptionsDto(Device: "Epson DS-530"));

        Assert.Equal("Epson DS-530", factory.Backend.LastOptions?.Device);
    }

    [Fact]
    public async Task A_scan_naming_a_device_that_is_not_there_fails_rather_than_using_another()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        // The scan runs behind StartScan, so a bad device lands as a failed session rather than
        // a rejected call — the same way a jam does. Failing is the point: scanning the wrong
        // machine is worse than not scanning.
        SessionDto done = await hub.ScanToEnd(new ScanOptionsDto(Device: "Nothing Like This"));

        Assert.Equal(SessionState.Failed, done.State);
        Assert.Empty(done.Pages);
        Assert.Contains("Nothing Like This", done.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Device_is_ignored_when_the_config_does_not_open_it_up()
    {
        await using var factory = new YassFactory(
            FakeScenario.Pages(1) with { ExtraDevices = ["Epson DS-530"] },
            config => config with { Scanner = config.Scanner with { UiEditable = ["dpi"] } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        await hub.StartScan(new ScanOptionsDto(Device: "Epson DS-530"));

        Assert.Equal("", factory.Backend.LastOptions?.Device);
    }
}
