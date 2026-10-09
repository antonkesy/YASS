using Microsoft.Extensions.DependencyInjection;
using Yass.App.Rendering;
using Yass.Core.Hub;

namespace Yass.Desktop.Tests;

public sealed class AppSchemeTests : IAsyncLifetime
{
    private DesktopHarness _desktop = null!;

    public ValueTask InitializeAsync()
    {
        _desktop = new DesktopHarness();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _desktop.DisposeAsync();

    private SchemeResponse? Get(string pathAndQuery) => _desktop.Scheme.Resolve(new Uri(_desktop.Scheme.Origin.Root, pathAndQuery));

    private static string Text(SchemeResponse response)
    {
        using var reader = new StreamReader(response.Body);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/session/abc")]
    [InlineData("/settings")]
    public void Root_and_client_routes_get_index_html(string path)
    {
        SchemeResponse response = Assert.IsType<SchemeResponse>(Get(path));
        Assert.StartsWith("text/html", response.ContentType, StringComparison.Ordinal);
        Assert.Contains("<title>YASS</title>", Text(response), StringComparison.Ordinal);
    }

    [Fact]
    public void Static_files_are_served_with_their_type()
    {
        SchemeResponse response = Assert.IsType<SchemeResponse>(Get("/_app/start.js"));
        Assert.StartsWith("text/javascript", response.ContentType, StringComparison.Ordinal);
        Assert.Equal("export {};", Text(response));
    }

    [Theory]
    [InlineData("/_app/missing.js")]
    [InlineData("/..%2F..%2F..%2Fetc%2Fpasswd")]
    [InlineData("/_app/..%2F..%2Fsessions")]
    [InlineData("/img/nope/0/thumb")]
    [InlineData("/export/nope/abc.pdf")]
    public void Missing_files_and_escapes_are_nothing(string path) => Assert.Null(Get(path));

    [Fact]
    public void Dot_segments_are_resolved_before_the_lookup_and_stay_inside_the_build()
    {
        SchemeResponse response = Assert.IsType<SchemeResponse>(Get("/%2E%2E/%2E%2E/etc/passwd"));
        Assert.Contains("<title>YASS</title>", Text(response), StringComparison.Ordinal);
    }

    [Fact]
    public void Other_schemes_are_nothing() => Assert.Null(_desktop.Scheme.Resolve(new Uri("https://example.com/")));

    [Fact]
    public async Task Page_images_are_webp_for_the_current_hash_and_nothing_for_an_unknown_one()
    {
        SessionDto session = await _desktop.ScanAsync();
        PageDto page = session.Pages[0];

        foreach (string kind in new[] { "thumb", "preview", "frame" })
        {
            SchemeResponse image = Assert.IsType<SchemeResponse>(Get($"/img/{session.Id}/0/{kind}?h={page.Hash}"));
            Assert.Equal("image/webp", image.ContentType);
            using Stream body = image.Body;
            byte[] header = new byte[12];
            await body.ReadExactlyAsync(header);
            Assert.Equal("WEBP"u8.ToArray(), header[8..12]);
        }

        Assert.Null(Get($"/img/{session.Id}/0/thumb?h={new string('0', page.Hash.Length)}"));
        Assert.Null(Get($"/img/{session.Id}/0/poster?h={page.Hash}"));
        Assert.Null(Get($"/img/{session.Id}/x/thumb?h={page.Hash}"));
    }

    [Fact]
    public async Task Previews_are_rendered_before_the_webview_asks()
    {
        SessionDto session = await _desktop.ScanAsync();
        RenderCache cache = _desktop.Services.GetRequiredService<RenderCache>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (PageDto page in session.Pages)
        {
            while (!File.Exists(cache.PathFor(session.Id, page.Hash, ImageKind.Preview)))
            {
                await Task.Delay(50, timeout.Token);
            }
        }
    }

    [Fact]
    public async Task An_export_is_served_once_and_then_gone()
    {
        SessionDto session = await _desktop.ScanAsync();
        ExportResult result = await _desktop.Api.Export(session.Id, new ExportRequest());
        string path = Assert.Single(result.Documents).DownloadPath;

        SchemeResponse first = Assert.IsType<SchemeResponse>(Get(path));
        Assert.Equal("application/pdf", first.ContentType);
        using (Stream body = first.Body)
        {
            byte[] magic = new byte[5];
            await body.ReadExactlyAsync(magic);
            Assert.Equal("%PDF-"u8.ToArray(), magic);
        }

        Assert.Null(Get(path));
    }

    [Fact]
    public async Task On_windows_the_app_is_an_https_host_and_other_hosts_are_left_alone()
    {
        await using var windows = new DesktopHarness(origin: AppOrigin.Windows);
        SchemeResponse index = Assert.IsType<SchemeResponse>(windows.Scheme.Resolve(new Uri("https://yass.localhost/")));
        Assert.StartsWith("text/html", index.ContentType, StringComparison.Ordinal);
        await index.Body.DisposeAsync();

        Assert.Null(windows.Scheme.Resolve(new Uri("https://example.com/")));
        Assert.Null(windows.Scheme.Resolve(new Uri("yass://app/")));
        Assert.Null(_desktop.Scheme.Resolve(new Uri("https://yass.localhost/")));
    }

    [Fact]
    public void Each_platform_gets_its_origin()
    {
        Assert.Equal(new Uri("yass://app/"), AppOrigin.Custom.Root);
        Assert.Equal(new Uri("https://yass.localhost/"), AppOrigin.Windows.Root);
        Assert.Equal(OperatingSystem.IsWindows() ? AppOrigin.Windows : AppOrigin.Custom, AppOrigin.ForThisPlatform());
        Assert.True(AppOrigin.Windows.Owns(new Uri("HTTPS://YASS.LOCALHOST/img/x")));
    }
}
