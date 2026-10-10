using Microsoft.Extensions.DependencyInjection;
using Yass.App.Api;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ScanScenarioTests
{
    [Fact]
    public async Task Error_after_k_pages_keeps_pages_and_reports_error()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(5) with { ErrorAfter = 2 });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto done = await hub.ScanToEnd();

        Assert.Equal(SessionState.Review, done.State);
        Assert.Equal(2, done.Pages.Length);
        Assert.Contains("jammed", done.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Of_two_scans_started_at_once_only_one_runs()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3) with { PageDelay = TimeSpan.FromMilliseconds(100) });
        YassApi api = factory.Services.GetRequiredService<YassApi>();

        Task<SessionDto>[] starts = [.. Enumerable.Range(0, 8).Select(_ => Task.Run(() => api.StartScan(null)))];
        await Task.WhenAll(starts.Select(t => t.ContinueWith(_ => { }, TaskScheduler.Default)));

        SessionDto started = await Assert.Single(starts, t => t.IsCompletedSuccessfully);
        Assert.All(starts.Where(t => !t.IsCompletedSuccessfully), t => Assert.IsType<YassException>(t.Exception!.InnerException));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        await hub.WaitFor(started.Id, s => s.State == SessionState.Review);
        Assert.Single(await hub.ListSessions());
    }

    [Fact]
    public async Task Error_before_any_page_fails_the_session()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(5) with { ErrorAfter = 0 });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto done = await hub.ScanToEnd();

        Assert.Equal(SessionState.Failed, done.State);
        Assert.Empty(done.Pages);
    }

    [Fact]
    public async Task Stop_scan_ends_early_in_review_with_pages_so_far()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(50) with { PageDelay = TimeSpan.FromMilliseconds(40) });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto started = await hub.StartScan();
        await hub.WaitFor(started.Id, s => s.Pages.Length >= 2);
        await hub.StopScan(started.Id);
        SessionDto done = await hub.WaitFor(started.Id, s => s.State != SessionState.Scanning);

        Assert.Equal(SessionState.Review, done.State);
        Assert.InRange(done.Pages.Length, 2, 10);
        Assert.Null(done.Error);
    }

    [Fact]
    public async Task Flatbed_source_yields_a_single_page()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(5));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto done = await hub.ScanToEnd(new ScanOptionsDto(Source: ScanSource.Flatbed));

        Assert.Single(done.Pages);
    }

    [Fact]
    public async Task Blank_detection_off_keeps_blank_pages()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3, 2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto done = await hub.ScanToEnd(new ScanOptionsDto(BlankPageDetection: false));

        Assert.All(done.Pages, p => Assert.False(p.IsBlank));
        Assert.All(done.Pages, p => Assert.False(p.Deleted));
    }

    [Fact]
    public async Task No_device_scenario_fails_scan_and_reports_status()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3) with { NoDevice = true });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        SessionDto done = await hub.ScanToEnd();

        Assert.Equal(SessionState.Failed, done.State);
        Assert.Contains("No scanner", done.Error, StringComparison.Ordinal);
    }
}
