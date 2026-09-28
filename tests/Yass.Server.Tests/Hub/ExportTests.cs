using System.Net;
using Microsoft.AspNetCore.SignalR;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ExportTests
{
    [Fact]
    public async Task Save_writes_pdf_to_consume_directory_and_finishes_session()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3, 2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, ExportMode.Save);

        Assert.Equal(ExportMode.Save, result.Mode);
        Assert.Equal(2, result.PageCount);
        Assert.StartsWith(factory.ConsumeDirectory, result.SavedPath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.SavedPath));
        Assert.Empty(Directory.GetFiles(factory.ConsumeDirectory, "*.part"));
        string pdf = await File.ReadAllTextAsync(result.SavedPath!);
        Assert.StartsWith("%PDF", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("page-002.png", pdf, StringComparison.Ordinal);
        Assert.Contains(hub.Progress, p => p.SessionId == done.Id && p.Percent == 100);
        Assert.Equal(SessionState.Done, (await hub.GetSession(done.Id)).State);
    }

    [Fact]
    public async Task Export_and_download_each_record_an_event_with_the_saved_path()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        Assert.Empty(scanned.Events);

        ExportResult saved = await hub.Export(scanned.Id, ExportMode.Save);
        SessionDto exported = await hub.GetSession(scanned.Id);
        SessionEventDto only = Assert.Single(exported.Events);
        Assert.Equal(SessionEventKind.Exported, only.Kind);
        Assert.Equal(saved.SavedPath, only.Path);

        await hub.Export(scanned.Id, ExportMode.Download);
        SessionDto downloaded = await hub.GetSession(scanned.Id);

        // History is append-only: the newest entry is the status, the earlier one survives.
        Assert.Equal(
            [SessionEventKind.Exported, SessionEventKind.Downloaded],
            downloaded.Events.Select(e => e.Kind));
        Assert.Null(downloaded.Events[1].Path);
        Assert.True(downloaded.SizeBytes > 0);
    }

    [Fact]
    public async Task Download_is_one_shot()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        HttpClient http = factory.CreateClient();
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, ExportMode.Download);

        Assert.Matches("^/export/.+/[0-9a-f]{32}\\.pdf$", result.DownloadPath);
        HttpResponseMessage first = await http.GetAsync(new Uri(result.DownloadPath!, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/pdf", first.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", first.Content.Headers.ContentDisposition!.DispositionType);
        Assert.StartsWith("%PDF", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(result.DownloadPath!, UriKind.Relative))).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(factory.Config.Sessions.Directory, done.Id, "export")));
    }

    [Fact]
    public async Task Download_disabled_by_config_is_rejected()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1), c => c with { Output = c.Output with { AllowDownload = false } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.Export(done.Id, ExportMode.Download));

        Assert.Contains("allow_download", ex.Message, StringComparison.Ordinal);
        Assert.Equal(SessionState.Review, (await hub.GetSession(done.Id)).State);
    }

    [Fact]
    public async Task Export_with_every_page_deleted_is_rejected()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();
        await hub.ApplyEdits(done.Id, PageEdit.Delete());

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.Export(done.Id, ExportMode.Save));

        Assert.Contains("No pages", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_success_delete_session_removes_it_after_save()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2), c => c with { Output = c.Output with { OnSuccess = OnSuccessAction.DeleteSession } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, ExportMode.Save);

        Assert.True(File.Exists(result.SavedPath));
        Assert.DoesNotContain(await hub.ListSessions(), s => s.Id == done.Id);
    }
}
