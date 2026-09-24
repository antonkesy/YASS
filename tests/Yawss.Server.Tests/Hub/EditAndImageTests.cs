using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Yawss.Core.Domain;
using Yawss.Core.Hub;
using Yawss.Fake;

namespace Yawss.Server.Tests.Hub;

public sealed class EditAndImageTests : IAsyncLifetime
{
    private readonly YawssFactory _factory = new(FakeScenario.Pages(3, 2));
    private HubClient _hub = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        _hub = await HubClient.ConnectAsync(_factory);
        _http = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await _hub.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Edits_change_hashes_push_page_updates_and_persist()
    {
        SessionDto done = await _hub.ScanToEnd();
        string hashBefore = done.Pages[0].Hash;

        SessionDto edited = await _hub.ApplyEdits(done.Id, PageEdit.Rotate(90, 0), PageEdit.Restore(1), PageEdit.SetColor(ColorMode.Color, 2));

        Assert.Equal(Rotation.R90, edited.Pages[0].Rotation);
        Assert.NotEqual(hashBefore, edited.Pages[0].Hash);
        Assert.False(edited.Pages[1].Deleted);
        Assert.Equal(ColorMode.Color, edited.Pages[2].ColorOverride);
        Assert.Contains(_hub.PagesUpdated, p => p.SessionId == done.Id && p.Page.Index == 0 && p.Page.Rotation == Rotation.R90);

        SessionDto reloaded = await _hub.GetSession(done.Id);
        Assert.Equal(edited.Pages.Select(p => p.Hash), reloaded.Pages.Select(p => p.Hash));
    }

    [Fact]
    public async Task Reorder_pushes_session_changed_and_reindexes()
    {
        SessionDto done = await _hub.ScanToEnd();
        string lastHash = done.Pages[2].Hash;

        SessionDto edited = await _hub.ApplyEdits(done.Id, PageEdit.Reorder(2, 0, 1));

        Assert.Equal(lastHash, edited.Pages[0].Hash);
        Assert.Equal([0, 1, 2], edited.Pages.Select(p => p.Index));
        Assert.Contains(_hub.SessionChanges, s => s.Id == done.Id && s.Pages.Length == 3 && s.Pages[0].Hash == lastHash);
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
        await using var factory = new YawssFactory(FakeScenario.Pages(20) with { PageDelay = TimeSpan.FromMilliseconds(50) });
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
