using Microsoft.AspNetCore.SignalR;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Core.Scanning;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ScanFlowTests : IAsyncLifetime
{
    private readonly YassFactory _factory = new(FakeScenario.Pages(4, 2));
    private HubClient _hub = null!;

    public async ValueTask InitializeAsync() => _hub = await HubClient.ConnectAsync(_factory);

    public async ValueTask DisposeAsync()
    {
        await _hub.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Healthz_answers_ok() =>
        Assert.Equal("ok", await _factory.CreateClient().GetStringAsync(new Uri("/healthz", UriKind.Relative)));

    [Fact]
    public async Task Scanner_status_reports_fake_device_ready()
    {
        ScannerStatusDto status = await _hub.GetScannerStatus();
        for (int i = 0; i < 40 && status.State == ScannerState.Unknown; i++)
        {
            await Task.Delay(50);
            status = await _hub.GetScannerStatus();
        }

        Assert.Equal(ScannerState.Ready, status.State);
        Assert.Equal(_factory.Scenario.DeviceName, status.DeviceName);
    }

    [Fact]
    public async Task Effective_options_expose_the_defaults()
    {
        ScanOptionsDto options = await _hub.GetEffectiveOptions();

        Assert.Equal(300, options.Dpi);
        Assert.Equal(ColorMode.Grayscale, options.Color);
    }

    [Fact]
    public async Task Scan_runs_until_feeder_empty_and_flags_blank_page_deleted()
    {
        SessionDto done = await _hub.ScanToEnd();

        Assert.Equal(SessionState.Review, done.State);
        Assert.Equal(4, done.Pages.Length);
        Assert.Equal([0, 1, 2, 3], done.Pages.Select(p => p.Index));
        Assert.True(done.Pages[1].IsBlank);
        Assert.True(done.Pages[1].Deleted);
        Assert.All(done.Pages.Where(p => p.Index != 1), p => Assert.False(p.Deleted));
        Assert.Equal(4, _hub.PagesAdded.Count(p => p.SessionId == done.Id));
        Assert.Contains(_hub.SessionChanges, s => s.Id == done.Id && s.State == SessionState.Review);
    }

    [Fact]
    public async Task Every_override_is_honoured_and_the_rest_stays_at_the_defaults()
    {
        SessionDto session = await _hub.StartScan(new ScanOptionsDto(Dpi: 150, Brightness: 50, Color: ColorMode.Color));
        await _hub.WaitFor(session.Id, s => s.State != SessionState.Scanning);

        Assert.Equal(150, session.OptionsUsed.Dpi);
        Assert.Equal(ColorMode.Color, session.OptionsUsed.Color);
        // Brightness is not in the default ui_editable list; the key no longer gates anything.
        Assert.Equal(50, session.OptionsUsed.Brightness);
        Assert.Equal(0, session.OptionsUsed.Contrast);
        Assert.Equal(150, _factory.Backend.LastOptions!.Dpi);
    }

    [Fact]
    public async Task Second_scan_while_one_runs_is_rejected()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(20) with { PageDelay = TimeSpan.FromMilliseconds(50) });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto first = await hub.StartScan();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.StartScan());
        Assert.Contains("already running", ex.Message, StringComparison.Ordinal);

        await hub.StopScan(first.Id);
        await hub.WaitFor(first.Id, s => s.State != SessionState.Scanning);
    }

    [Fact]
    public async Task Sessions_persist_on_disk_and_can_be_listed_and_deleted()
    {
        SessionDto done = await _hub.ScanToEnd();

        string manifest = Path.Combine(_factory.Config.Sessions.Directory, done.Id, "session.json");
        Assert.True(File.Exists(manifest));
        Assert.True(File.Exists(Path.Combine(_factory.Config.Sessions.Directory, done.Id, "page-001.png")));
        Assert.Contains(await _hub.ListSessions(), s => s.Id == done.Id);

        await _hub.DeleteSession(done.Id);

        Assert.DoesNotContain(await _hub.ListSessions(), s => s.Id == done.Id);
        Assert.False(Directory.Exists(Path.Combine(_factory.Config.Sessions.Directory, done.Id)));
        await Assert.ThrowsAsync<HubException>(() => _hub.GetSession(done.Id));
    }

    [Fact]
    public async Task Unknown_session_is_a_hub_error()
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetSession("../etc"));
        Assert.Contains("not found", ex.Message, StringComparison.Ordinal);
    }
}
