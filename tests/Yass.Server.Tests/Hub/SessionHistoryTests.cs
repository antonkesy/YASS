using Microsoft.AspNetCore.SignalR;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class SessionHistoryTests
{
    [Fact]
    public async Task Rename_sets_and_clears_the_name()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        Assert.Null(scanned.Name);
        Assert.Equal("  Tax return 2025  ".Trim(), (await hub.RenameSession(scanned.Id, "  Tax return 2025  ")).Name);
        Assert.Equal("Tax return 2025", (await hub.GetSession(scanned.Id)).Name);

        // Blank clears it, so the UI falls back to the timestamp.
        Assert.Null((await hub.RenameSession(scanned.Id, "   ")).Name);
    }

    [Fact]
    public async Task Rename_rejects_an_overlong_name()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        await Assert.ThrowsAsync<HubException>(() => hub.RenameSession(scanned.Id, new string('x', 201)));
    }

    [Fact]
    public async Task Discard_keeps_the_session_listed_but_drops_its_pages()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();
        string dir = Path.Combine(factory.Config.Sessions.Directory, scanned.Id);

        SessionDto discarded = await hub.DiscardSession(scanned.Id);

        Assert.Equal(SessionEventKind.Discarded, Assert.Single(discarded.Events).Kind);
        Assert.Empty(discarded.Pages);
        Assert.Contains(await hub.ListSessions(), s => s.Id == scanned.Id);
        Assert.Empty(Directory.GetFiles(dir, "page-*.png"));
        Assert.True(File.Exists(Path.Combine(dir, "session.json")));
    }

    [Fact]
    public async Task Discard_is_refused_while_a_scan_is_running()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(50) with { PageDelay = TimeSpan.FromMilliseconds(40) });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto started = await hub.StartScan();

        await Assert.ThrowsAsync<HubException>(() => hub.DiscardSession(started.Id));

        await hub.StopScan(started.Id);
    }

    [Fact]
    public async Task Clear_removes_every_idle_session()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        await hub.ScanToEnd();
        await hub.ScanToEnd();

        Assert.Equal(2, await hub.ClearSessions());
        Assert.Empty(await hub.ListSessions());
    }
}
