using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Yass.App.Api;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ExportTests
{
    private static async Task<string> Fetch(YassFactory factory, string downloadPath)
    {
        HttpClient http = factory.CreateClient();
        HttpResponseMessage response = await http.GetAsync(new Uri(downloadPath, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Export_produces_a_pdf_of_the_active_pages_and_finishes_the_session()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(3, 2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id);

        Assert.Equal(2, result.PageCount);
        string pdf = await Fetch(factory, Assert.Single(result.Documents).DownloadPath);
        Assert.StartsWith("%PDF", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("page-002.png", pdf, StringComparison.Ordinal);
        Assert.Contains(hub.Progress, p => p.SessionId == done.Id && p.Percent == 100);
        Assert.Equal(SessionState.Done, (await hub.GetSession(done.Id)).State);
    }

    [Fact]
    public async Task A_cancelled_export_returns_the_session_to_review_and_leaves_no_file()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();
        YassApi api = factory.Services.GetRequiredService<YassApi>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => api.Export(done.Id, new ExportRequest(), new CancellationToken(canceled: true)));

        SessionDto after = await hub.GetSession(done.Id);
        Assert.Equal(SessionState.Review, after.State);
        Assert.Null(after.Error);
        string exports = Path.Combine(factory.Root, "sessions", done.Id, "export");
        Assert.Empty(Directory.Exists(exports) ? Directory.EnumerateFiles(exports) : []);

        // And it can be exported again.
        Assert.Equal(2, (await hub.Export(done.Id)).PageCount);
    }

    [Fact]
    public async Task Each_export_records_a_download_event()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        Assert.Empty(scanned.Events);

        await hub.Export(scanned.Id);
        SessionDto once = await hub.GetSession(scanned.Id);
        SessionEventDto only = Assert.Single(once.Events);
        Assert.Equal(SessionEventKind.Downloaded, only.Kind);
        Assert.Null(only.Path);

        await hub.Export(scanned.Id);
        SessionDto twice = await hub.GetSession(scanned.Id);

        // History is append-only: the newest entry is the status, the earlier one survives.
        Assert.Equal(
            [SessionEventKind.Downloaded, SessionEventKind.Downloaded],
            twice.Events.Select(e => e.Kind));
        Assert.True(twice.SizeBytes > 0);
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
        ExportResult result = await hub.Export(scanned.Id);

        Assert.Equal(2, result.Documents.Length);
        Assert.Equal([2, 2], result.Documents.Select(d => d.PageCount));
        Assert.Equal(4, result.PageCount);

        // Two downloads, not one handed out twice.
        Assert.Equal(2, result.Documents.Select(d => d.DownloadPath).Distinct(StringComparer.Ordinal).Count());
        foreach (ExportedDocumentDto document in result.Documents)
        {
            Assert.StartsWith("%PDF", await Fetch(factory, document.DownloadPath), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_split_on_the_first_page_does_not_make_an_empty_document()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        await hub.ApplyEdits(scanned.Id, PageEdit.SetSplit(true, 0));
        ExportResult result = await hub.Export(scanned.Id);

        Assert.Single(result.Documents);
    }

    [Fact]
    public async Task Metadata_from_the_request_lands_in_the_pdf_under_the_document_name()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto scanned = await hub.ScanToEnd();

        ExportResult result = await hub.Export(
            scanned.Id,
            new ExportRequest(ExportFormat.Pdf, "Invoice {YEAR}", new DocumentMetadataDto("Jane Doe", "Invoices", "invoice, 2026")));

        string pdf = await Fetch(factory, Assert.Single(result.Documents).DownloadPath);
        Assert.Matches(@"/Title\s*\(Invoice \d{4}\)", pdf);
        Assert.Matches(@"/Author\s*\(Jane Doe\)", pdf);
        Assert.Matches(@"/Subject\s*\(Invoices\)", pdf);
        Assert.Matches(@"/Keywords\s*\(invoice, 2026\)", pdf);
    }

    [Fact]
    public async Task Download_is_one_shot()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        HttpClient http = factory.CreateClient();
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id);

        Assert.Matches("^/export/.+/[0-9a-f]{32}\\.pdf$", Assert.Single(result.Documents).DownloadPath);
        HttpResponseMessage first = await http.GetAsync(new Uri(Assert.Single(result.Documents).DownloadPath, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/pdf", first.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", first.Content.Headers.ContentDisposition!.DispositionType);
        Assert.StartsWith("%PDF", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(Assert.Single(result.Documents).DownloadPath, UriKind.Relative))).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(factory.Config.Sessions.Directory, done.Id, "export")));
    }

    [Fact]
    public async Task Download_serves_an_image_export_under_its_own_extension()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        HttpClient http = factory.CreateClient();
        SessionDto done = await hub.ScanToEnd();

        ExportResult result = await hub.Export(done.Id, new ExportRequest(ExportFormat.Jpg));

        string url = Assert.Single(result.Documents).DownloadPath;
        Assert.EndsWith(".jpg", url, StringComparison.Ordinal);
        HttpResponseMessage response = await http.GetAsync(new Uri(url, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".jpg", response.Content.Headers.ContentDisposition!.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_with_every_page_deleted_is_rejected()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();
        await hub.ApplyEdits(done.Id, PageEdit.Delete());

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.Export(done.Id));

        Assert.Contains("No pages", ex.Message, StringComparison.Ordinal);
    }

    private static YassFactory WithServerFolder(int pages, out string folder)
    {
        string root = Path.Combine(Path.GetTempPath(), "yass-tests", Guid.NewGuid().ToString("N"), "consume");
        folder = root;
        return new YassFactory(FakeScenario.Pages(pages), c => c with { Output = c.Output with { Directory = root } });
    }

    [Fact]
    public async Task Saving_to_the_server_writes_the_named_document_into_the_folder()
    {
        await using YassFactory factory = WithServerFolder(2, out string folder);
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        Assert.Equal(folder, await hub.GetServerFolder());
        ExportResult result = await hub.Export(done.Id, new ExportRequest(Name: "Tax/2026: letter", ToServer: true));

        ExportedDocumentDto document = Assert.Single(result.Documents);
        Assert.Equal("", document.DownloadPath);
        // The slash and colon cannot climb out of the folder or upset the file system.
        Assert.Equal(Path.Combine(folder, "Tax_2026_ letter.pdf"), document.SavedTo);
        Assert.StartsWith("%PDF", await File.ReadAllTextAsync(document.SavedTo!), StringComparison.Ordinal);
        Assert.Equal([document.SavedTo], Directory.GetFiles(folder));

        SessionDto after = await hub.GetSession(done.Id);
        Assert.Equal(SessionState.Done, after.State);
        SessionEventDto saved = after.Events[^1];
        Assert.Equal(SessionEventKind.Exported, saved.Kind);
        Assert.Equal(document.SavedTo, saved.Path);
        Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
    }

    [Fact]
    public async Task Saving_twice_under_one_name_never_overwrites()
    {
        await using YassFactory factory = WithServerFolder(1, out string folder);
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        ExportResult first = await hub.Export(done.Id, new ExportRequest(Name: "letter", ToServer: true));
        ExportResult second = await hub.Export(done.Id, new ExportRequest(Name: "letter", ToServer: true));

        Assert.Equal(Path.Combine(folder, "letter.pdf"), first.Documents[0].SavedTo);
        Assert.Equal(Path.Combine(folder, "letter-2.pdf"), second.Documents[0].SavedTo);
        Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
    }

    [Fact]
    public async Task Saving_to_the_server_is_refused_while_no_folder_is_configured()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        Assert.Equal("", await hub.GetServerFolder());
        await Assert.ThrowsAsync<HubException>(() => hub.Export(done.Id, new ExportRequest(ToServer: true)));
        // Refused before it started, so the scan is still there to be saved another way.
        Assert.Equal(SessionState.Review, (await hub.GetSession(done.Id)).State);
    }
}
