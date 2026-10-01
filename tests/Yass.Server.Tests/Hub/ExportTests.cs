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
        Assert.StartsWith(factory.ConsumeDirectory, Assert.Single(result.Documents).SavedPath, StringComparison.Ordinal);
        Assert.True(File.Exists(Assert.Single(result.Documents).SavedPath));
        Assert.Empty(Directory.GetFiles(factory.ConsumeDirectory, "*.part"));
        string pdf = await File.ReadAllTextAsync(Assert.Single(result.Documents).SavedPath!);
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
        Assert.Equal(Assert.Single(saved.Documents).SavedPath, only.Path);

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
    public async Task A_split_produces_one_document_per_part()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(4));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        // Page 3 opens a second document, so 4 pages become 2 + 2.
        SessionDto split = await hub.ApplyEdits(scanned.Id, PageEdit.SetSplit(true, 2));
        Assert.Equal(4, split.Pages.Length);
        Assert.True(split.Pages[2].StartsDocument, "page 3 should be marked as a document start");
        ExportResult result = await hub.Export(scanned.Id, ExportMode.Save);

        Assert.Equal(2, result.Documents.Length);
        Assert.Equal([2, 2], result.Documents.Select(d => d.PageCount));
        Assert.Equal(4, result.PageCount);
        foreach (ExportedDocumentDto document in result.Documents)
        {
            Assert.True(File.Exists(document.SavedPath), $"{document.SavedPath} should exist");
        }

        // Two files, not one overwritten twice.
        Assert.Equal(2, result.Documents.Select(d => d.SavedPath).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task A_split_on_the_first_page_does_not_make_an_empty_document()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        await hub.ApplyEdits(scanned.Id, PageEdit.SetSplit(true, 0));
        ExportResult result = await hub.Export(scanned.Id, ExportMode.Save);

        Assert.Single(result.Documents);
    }

    [Fact]
    public async Task A_chosen_name_and_folder_are_used()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();
        string elsewhere = Path.Combine(factory.ConsumeDirectory, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        ExportResult result = await hub.Export(
            scanned.Id,
            new ExportRequest(ExportMode.Save, ExportFormat.Pdf, "Tax return {YEAR}", elsewhere));

        string saved = Assert.Single(result.Documents).SavedPath!;
        Assert.Equal(elsewhere, Path.GetDirectoryName(saved));
        Assert.Matches(@"^Tax return \d{4}\.pdf$", Path.GetFileName(saved));
    }

    [Fact]
    public async Task Metadata_from_the_request_lands_in_the_saved_pdf_under_the_document_name()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        ExportResult result = await hub.Export(
            scanned.Id,
            new ExportRequest(ExportMode.Save, ExportFormat.Pdf, "Invoice", null, new DocumentMetadataDto("Jane Doe", "Invoices", "invoice, 2026")));

        string pdf = await File.ReadAllTextAsync(Assert.Single(result.Documents).SavedPath!);
        Assert.Matches(@"/Title\s*\(Invoice\)", pdf);
        Assert.Matches(@"/Author\s*\(Jane Doe\)", pdf);
        Assert.Matches(@"/Subject\s*\(Invoices\)", pdf);
        Assert.Matches(@"/Keywords\s*\(invoice, 2026\)", pdf);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("/does/not/exist/anywhere")]
    public async Task An_unusable_export_folder_is_refused(string directory)
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        var ex = await Assert.ThrowsAsync<HubException>(
            () => hub.Export(scanned.Id, new ExportRequest(ExportMode.Save, ExportFormat.Pdf, null, directory)));

        Assert.Contains(directory, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_export_folder_outside_the_allowed_roots_is_refused()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        // Exists and is writable, but sits beside the consume directory rather than inside it.
        string sibling = factory.ConsumeDirectory + "-elsewhere";
        Directory.CreateDirectory(sibling);

        var ex = await Assert.ThrowsAsync<HubException>(
            () => hub.Export(scanned.Id, new ExportRequest(ExportMode.Save, ExportFormat.Pdf, null, sibling)));

        Assert.Contains("allowed roots", ex.Message, StringComparison.Ordinal);
        Assert.Contains("output.allowed_roots", ex.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(sibling));

        // A refused folder must leave the session exactly as it was, not stuck in Exporting.
        SessionDto after = await hub.GetSession(scanned.Id);
        Assert.Equal(SessionState.Review, after.State);
        Assert.Empty(after.Events);

        // And the session is still exportable to somewhere that is allowed.
        Assert.Single((await hub.Export(scanned.Id, ExportMode.Save)).Documents);
    }

    [Fact]
    public async Task A_symlink_out_of_the_allowed_roots_is_refused()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        // Looks contained — it sits inside the consume directory — but points outside it.
        string outside = Directory.CreateTempSubdirectory("yass-outside").FullName;
        Directory.CreateDirectory(factory.ConsumeDirectory);
        string link = Path.Combine(factory.ConsumeDirectory, "sneaky");
        Directory.CreateSymbolicLink(link, outside);

        var ex = await Assert.ThrowsAsync<HubException>(
            () => hub.Export(scanned.Id, new ExportRequest(ExportMode.Save, ExportFormat.Pdf, null, link)));

        Assert.Contains("allowed roots", ex.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(outside));

        Directory.Delete(outside, recursive: true);
    }

    [Fact]
    public async Task Configured_roots_widen_where_the_ui_may_export()
    {
        string shared = Directory.CreateTempSubdirectory("yass-allowed").FullName;
        await using var factory = new YassFactory(
            FakeScenario.Pages(1),
            c => c with { Output = c.Output with { AllowedRoots = [shared] } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();
        string inside = Path.Combine(shared, "invoices");
        Directory.CreateDirectory(inside);

        ExportResult result = await hub.Export(
            scanned.Id,
            new ExportRequest(ExportMode.Save, ExportFormat.Pdf, null, inside));

        Assert.Equal(inside, Path.GetDirectoryName(Assert.Single(result.Documents).SavedPath));

        // Naming a root does not keep the default consume directory allowed.
        await Assert.ThrowsAsync<HubException>(
            () => hub.Export(scanned.Id, new ExportRequest(ExportMode.Save, ExportFormat.Pdf, null, factory.ConsumeDirectory)));

        Directory.Delete(shared, recursive: true);
    }

    [Fact]
    public async Task The_ui_is_told_which_roots_are_in_force()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        ScanOptionsDto options = await hub.GetEffectiveOptions();

        Assert.Equal([factory.ConsumeDirectory], Assert.IsType<string[]>(options.ExportRoots));
    }

    [Fact]
    public async Task Image_formats_write_one_file_per_page()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        ExportResult result = await hub.Export(
            scanned.Id,
            new ExportRequest(ExportMode.Save, ExportFormat.Png, "pages", null));

        Assert.Single(result.Documents);
        Assert.Equal(3, result.PageCount);
        string[] files = [.. Directory.GetFiles(factory.ConsumeDirectory, "*.png").Select(Path.GetFileName)!];
        Assert.Equal(["pages-01.png", "pages-02.png", "pages-03.png"], files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Download_is_one_shot()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        HttpClient http = factory.CreateClient();
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, ExportMode.Download);

        Assert.Matches("^/export/.+/[0-9a-f]{32}\\.pdf$", Assert.Single(result.Documents).DownloadPath);
        HttpResponseMessage first = await http.GetAsync(new Uri(Assert.Single(result.Documents).DownloadPath!, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/pdf", first.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", first.Content.Headers.ContentDisposition!.DispositionType);
        Assert.StartsWith("%PDF", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(Assert.Single(result.Documents).DownloadPath!, UriKind.Relative))).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(factory.Config.Sessions.Directory, done.Id, "export")));
    }

    [Fact]
    public async Task Download_serves_an_image_export_under_its_own_extension()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        HttpClient http = factory.CreateClient();
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, new ExportRequest(ExportMode.Download, ExportFormat.Jpg, null, null));

        string url = Assert.Single(result.Documents).DownloadPath!;
        Assert.EndsWith(".jpg", url, StringComparison.Ordinal);
        HttpResponseMessage response = await http.GetAsync(new Uri(url, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".jpg", response.Content.Headers.ContentDisposition!.FileName, StringComparison.Ordinal);
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

        Assert.True(File.Exists(Assert.Single(result.Documents).SavedPath));
        Assert.DoesNotContain(await hub.ListSessions(), s => s.Id == done.Id);
    }
}
