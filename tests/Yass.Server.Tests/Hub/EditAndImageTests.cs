using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yass.App.Sessions;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class EditAndImageTests : IAsyncLifetime
{
    private readonly YassFactory _factory = new(FakeScenario.Pages(3, 2));
    private HubClient _hub = null!;
    private HttpClient _http = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = await HubClient.ConnectAsync(_factory);
        _http = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _hub.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Edits_change_hashes_push_page_updates_and_persist()
    {
        SessionDto done = await _hub.ScanToEnd();
        string hashBefore = done.Pages[0].Hash;

        SessionDto edited = await _hub.ApplyEdits(done.Id, PageEdit.Rotate(90, 0), PageEdit.Restore(1), PageEdit.SetFilter(PageFilter.Bw, 2));

        Assert.Equal(Rotation.R90, edited.Pages[0].Rotation);
        Assert.NotEqual(hashBefore, edited.Pages[0].Hash);
        Assert.False(edited.Pages[1].Deleted);
        Assert.Equal(PageFilter.Bw, edited.Pages[2].Filter);
        Assert.Contains(_hub.PagesUpdated, p => p.SessionId == done.Id && p.Page.Index == 0 && p.Page.Rotation == Rotation.R90);

        SessionDto reloaded = await _hub.GetSession(done.Id);
        Assert.Equal(edited.Pages.Select(p => p.Hash), reloaded.Pages.Select(p => p.Hash));
    }

    [Fact]
    public async Task Crop_survives_the_round_trip_and_is_rendered()
    {
        SessionDto done = await _hub.ScanToEnd();
        string before = done.Pages[0].Hash;

        SessionDto cropped = await _hub.ApplyEdits(done.Id, PageEdit.SetCrop(new CropBox(10, 20, 10, 20), 0));

        CropBox box = Assert.IsType<CropBox>(cropped.Pages[0].Crop);
        Assert.Equal(10, box.Top);
        Assert.Equal(20, box.Right);
        Assert.NotEqual(before, cropped.Pages[0].Hash);

        HttpClient http = _factory.CreateClient();
        using var full = await Image.LoadAsync(await (await http.GetAsync(new Uri($"/img/{done.Id}/1/preview?h={done.Pages[1].Hash}", UriKind.Relative))).Content.ReadAsStreamAsync());
        using var trimmed = await Image.LoadAsync(await (await http.GetAsync(new Uri($"/img/{done.Id}/0/preview?h={cropped.Pages[0].Hash}", UriKind.Relative))).Content.ReadAsStreamAsync());
        Assert.True(trimmed.Width < full.Width, $"cropped {trimmed.Width} should be narrower than {full.Width}");
    }

    [Fact]
    public async Task The_crop_frame_shows_the_whole_page_with_its_other_edits()
    {
        SessionDto done = await _hub.ScanToEnd();
        SessionDto edited = await _hub.ApplyEdits(
            done.Id,
            PageEdit.SetCrop(new CropBox(10, 20, 10, 20), 0),
            PageEdit.SetSkew(5, 0),
            PageEdit.Rotate(90, 0),
            PageEdit.Rotate(90, 1));
        string hash = edited.Pages[0].Hash;

        using var full = await Image.LoadAsync(await _http.GetStreamAsync(new Uri($"/img/{done.Id}/1/preview?h={edited.Pages[1].Hash}", UriKind.Relative)));
        using var preview = await Image.LoadAsync(await _http.GetStreamAsync(new Uri($"/img/{done.Id}/0/preview?h={hash}", UriKind.Relative)));
        using var frame = await Image.LoadAsync(await _http.GetStreamAsync(new Uri($"/img/{done.Id}/0/frame?h={hash}", UriKind.Relative)));

        // Previews are scaled to a set width, so the crop shows in the proportions, not the width.
        Assert.NotEqual(Math.Round((double)full.Width / full.Height, 2), Math.Round((double)preview.Width / preview.Height, 2));
        Assert.Equal((full.Width, full.Height), (frame.Width, frame.Height));
        Assert.True(frame.Width > frame.Height, "the frame keeps the quarter turn");
    }

    [Fact]
    public async Task An_unknown_image_kind_is_not_found_rather_than_the_app()
    {
        SessionDto done = await _hub.ScanToEnd();

        HttpResponseMessage response = await _http.GetAsync(new Uri($"/img/{done.Id}/0/poster", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Auto_crop_trims_the_frame_off_the_pages_that_have_one()
    {
        SessionDto done = await _hub.ScanToEnd();
        // Thumbnails render in the background; wait them out so none is reading the page below.
        await _hub.WaitFor(done.Id, s => s.Pages.All(p => p.ThumbnailReady));
        // The fake scanner's pages are clean, so give page 0 the dark band a scanner lid leaves.
        SessionStore store = _factory.Services.GetRequiredService<SessionStore>();
        Session session = store.Get(done.Id);
        string source = SessionPaths.Source(store.Root, session, session.Pages[0]);
        using (Image<Rgb24> page = await Image.LoadAsync<Rgb24>(source))
        {
            int band = page.Height / 10;
            page.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < band; y++)
                {
                    rows.GetRowSpan(y).Fill(new Rgb24(15, 15, 15));
                }
            });
            await page.SaveAsPngAsync(source);
        }

        int cropped = await _hub.AutoCrop(done.Id);
        SessionDto after = await _hub.GetSession(done.Id);

        Assert.Equal(1, cropped);
        CropBox box = Assert.IsType<CropBox>(after.Pages[0].Crop);
        Assert.InRange(box.Top, 10, 12);
        Assert.Equal(0, box.Left);
        Assert.NotEqual(done.Pages[0].Hash, after.Pages[0].Hash);
        Assert.All(after.Pages.Skip(1), p => Assert.Null(p.Crop));
        Assert.Contains(_hub.PagesUpdated, p => p.SessionId == done.Id && p.Page.Index == 0 && p.Page.Crop != null);

        Assert.Equal(0, await _hub.AutoCrop(done.Id, 0));
        await Assert.ThrowsAsync<HubException>(() => _hub.AutoCrop(done.Id, 42));
    }

    [Fact]
    public async Task Rescan_replaces_one_page_and_keeps_its_edits()
    {
        SessionDto done = await _hub.ScanToEnd();
        SessionDto rotated = await _hub.ApplyEdits(done.Id, PageEdit.Rotate(90, 1), PageEdit.SetFilter(PageFilter.Bw, 1));
        Assert.True(rotated.Pages[1].Deleted, "the blank page starts out deleted");

        SessionDto after = await _hub.RescanPage(done.Id, 1);

        Assert.Equal(rotated.Pages.Length, after.Pages.Length);
        Assert.Equal(Rotation.R90, after.Pages[1].Rotation);
        Assert.Equal(PageFilter.Bw, after.Pages[1].Filter);
        Assert.False(after.Pages[1].Deleted);
        Assert.Equal(done.Pages[0].Hash, after.Pages[0].Hash);
    }

    [Fact]
    public async Task Scan_more_appends_pages_without_overwriting_the_first_run()
    {
        SessionDto first = await _hub.ScanToEnd();
        string[] before = [.. first.Pages.Select(p => p.Hash)];

        await _hub.ScanMore(first.Id);
        SessionDto after = await _hub.WaitFor(first.Id, s => s.State != SessionState.Scanning);

        Assert.Equal(first.Pages.Length * 2, after.Pages.Length);
        Assert.Equal(SessionState.Review, after.State);

        Assert.Equal(before, after.Pages.Take(first.Pages.Length).Select(p => p.Hash));
        string dir = Path.Combine(_factory.Config.Sessions.Directory, first.Id);
        Assert.Equal(after.Pages.Length, Directory.GetFiles(dir, "page-*.png").Length);
        Assert.Equal([.. Enumerable.Range(0, after.Pages.Length)], after.Pages.Select(p => p.Index));
    }

    [Fact]
    public async Task Rescan_is_refused_for_a_page_that_does_not_exist()
    {
        SessionDto done = await _hub.ScanToEnd();

        await Assert.ThrowsAsync<HubException>(() => _hub.RescanPage(done.Id, 99));
    }

    [Fact]
    public async Task An_impossible_crop_is_rejected()
    {
        SessionDto done = await _hub.ScanToEnd();

        await Assert.ThrowsAsync<HubException>(() => _hub.ApplyEdits(done.Id, PageEdit.SetCrop(new CropBox(60, 0, 60, 0), 0)));
    }

    [Fact]
    public async Task Reorder_pushes_session_changed_and_reindexes()
    {
        SessionDto done = await _hub.ScanToEnd();
        string lastHash = done.Pages[2].Hash;

        SessionDto edited = await _hub.ApplyEdits(done.Id, PageEdit.Reorder(2, 0, 1));

        Assert.Equal(lastHash, edited.Pages[0].Hash);
        Assert.Equal([0, 1, 2], edited.Pages.Select(p => p.Index));
        await _hub.WaitForPush(s => s.Id == done.Id && s.Pages.Length == 3 && s.Pages[0].Hash == lastHash);
    }

    [Fact]
    public async Task Invalid_edit_is_a_hub_error_and_changes_nothing()
    {
        SessionDto done = await _hub.ScanToEnd();

        await Assert.ThrowsAsync<HubException>(() => _hub.ApplyEdits(done.Id, PageEdit.Delete(42)));
        await Assert.ThrowsAsync<HubException>(() => _hub.ApplyEdits(done.Id, PageEdit.Reorder(0, 0, 1)));

        SessionDto after = await _hub.GetSession(done.Id);
        Assert.Equal(done.Pages.Select(p => p.Hash), after.Pages.Select(p => p.Hash));
    }

    [Fact]
    public async Task Edits_are_rejected_while_scanning()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(20) with { PageDelay = TimeSpan.FromMilliseconds(50) });
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto started = await hub.StartScan();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.ApplyEdits(started.Id, PageEdit.Rotate(90)));
        Assert.Contains("Scanning", ex.Message, StringComparison.Ordinal);

        await hub.StopScan(started.Id);
        await hub.WaitFor(started.Id, s => s.State != SessionState.Scanning);
    }

    [Fact]
    public async Task Thumb_and_preview_are_webp_immutable_and_etagged()
    {
        SessionDto done = await _hub.ScanToEnd();
        PageDto page = done.Pages[0];

        HttpResponseMessage thumb = await _http.GetAsync(new Uri($"/img/{done.Id}/0/thumb?h={page.Hash}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, thumb.StatusCode);
        Assert.Equal("image/webp", thumb.Content.Headers.ContentType!.MediaType);
        Assert.Contains("immutable", thumb.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal($"\"{page.Hash}\"", thumb.Headers.ETag!.Tag);
        byte[] bytes = await thumb.Content.ReadAsByteArrayAsync();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));

        using var conditional = new HttpRequestMessage(HttpMethod.Get, new Uri($"/img/{done.Id}/0/thumb?h={page.Hash}", UriKind.Relative));
        conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(thumb.Headers.ETag.Tag));
        Assert.Equal(HttpStatusCode.NotModified, (await _http.SendAsync(conditional)).StatusCode);

        HttpResponseMessage preview = await _http.GetAsync(new Uri($"/img/{done.Id}/0/preview", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.True((await preview.Content.ReadAsByteArrayAsync()).Length > bytes.Length);
    }

    [Fact]
    public async Task Rotated_page_renders_transposed_thumbnail()
    {
        SessionDto done = await _hub.ScanToEnd();
        SessionDto edited = await _hub.ApplyEdits(done.Id, PageEdit.Rotate(90, 0));

        byte[] portrait = await _http.GetByteArrayAsync(new Uri($"/img/{done.Id}/1/thumb?h={edited.Pages[1].Hash}", UriKind.Relative));
        byte[] landscape = await _http.GetByteArrayAsync(new Uri($"/img/{done.Id}/0/thumb?h={edited.Pages[0].Hash}", UriKind.Relative));

        using var p = SixLabors.ImageSharp.Image.Load(portrait);
        using var l = SixLabors.ImageSharp.Image.Load(landscape);
        Assert.True(p.Height > p.Width);
        Assert.True(l.Width > l.Height);
    }

    [Fact]
    public async Task Unknown_hash_or_session_is_not_found()
    {
        SessionDto done = await _hub.ScanToEnd();

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(new Uri($"/img/{done.Id}/0/thumb?h=0123456789abcdef", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(new Uri($"/img/{done.Id}/9/thumb", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(new Uri("/img/nope/0/thumb", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(new Uri($"/img/{done.Id}/0/thumb?h=../../x", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Thumbnails_become_ready_in_background_and_are_announced()
    {
        SessionDto done = await _hub.ScanToEnd();

        SessionDto ready = await _hub.WaitFor(done.Id, s => s.Pages.All(p => p.ThumbnailReady));

        Assert.All(ready.Pages, p => Assert.True(p.ThumbnailReady));
        Assert.Contains(_hub.PagesUpdated, p => p.SessionId == done.Id && p.Page.ThumbnailReady);
        Assert.True(File.Exists(Path.Combine(_factory.Config.Sessions.Directory, done.Id, "cache", $"{ready.Pages[0].Hash}-thumb.webp")));
    }
}
