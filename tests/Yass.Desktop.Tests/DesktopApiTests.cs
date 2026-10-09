using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yass.App.Export;
using Yass.Core.Hub;

namespace Yass.Desktop.Tests;

public sealed class DesktopApiTests : IAsyncLifetime
{
    private readonly FakeDialogs _dialogs = new();
    private DesktopHarness _desktop = null!;
    private DesktopApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _desktop = new DesktopHarness();
        _api = new DesktopApi(_dialogs, _desktop.Services.GetRequiredService<ExportDownloads>(), NullLogger<DesktopApi>.Instance);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _desktop.DisposeAsync();

    private string Out(string name) => Path.Combine(_desktop.Root, "out", name);

    private async Task<string> ExportAsync()
    {
        SessionDto session = await _desktop.ScanAsync();
        return Assert.Single((await _desktop.Api.Export(session.Id, new ExportRequest())).Documents).DownloadPath;
    }

    [Fact]
    public async Task The_save_dialog_gets_a_safe_name_and_a_typed_name_keeps_the_extension()
    {
        _dialogs.SaveAnswer = Out("letter");
        Assert.Equal(Out("letter.pdf"), await _api.PickSavePath("a/b letter.pdf"));
        Assert.Equal("a-b letter.pdf", Path.GetFileName(_dialogs.LastSuggested));
        Assert.Equal(".pdf", _dialogs.LastExtension);
    }

    [Fact]
    public async Task Cancelling_a_dialog_is_null()
    {
        Assert.Null(await _api.PickSavePath("scan.pdf"));
        Assert.Null(await _api.PickFolder());
    }

    [Fact]
    public async Task The_next_dialog_opens_where_the_last_one_saved()
    {
        _dialogs.FolderAnswer = Out("");
        Assert.Equal(Out(""), await _api.PickFolder());
        _dialogs.SaveAnswer = Out("x.pdf");
        await _api.PickSavePath("x.pdf");
        Assert.Equal(Out("x.pdf"), _dialogs.LastSuggested);
    }

    [Fact]
    public async Task An_export_is_moved_to_the_picked_file_once()
    {
        string path = await ExportAsync();
        string saved = _api.SaveExport(path, Out("scan.pdf"), overwrite: true);

        Assert.Equal(Out("scan.pdf"), saved);
        Assert.Equal("%PDF-"u8.ToArray(), (await File.ReadAllBytesAsync(saved))[..5]);
        Assert.Throws<BridgeException>(() => _api.SaveExport(path, Out("again.pdf"), overwrite: true));
    }

    [Fact]
    public async Task The_picked_file_is_replaced_but_a_folder_save_never_overwrites()
    {
        Directory.CreateDirectory(Out(""));
        await File.WriteAllTextAsync(Out("scan.pdf"), "old");

        Assert.Equal(Out("scan-2.pdf"), _api.SaveExport(await ExportAsync(), Out("scan.pdf"), overwrite: false));
        Assert.Equal("old", await File.ReadAllTextAsync(Out("scan.pdf")));

        Assert.Equal(Out("scan.pdf"), _api.SaveExport(await ExportAsync(), Out("scan.pdf"), overwrite: true));
        Assert.NotEqual("old", await File.ReadAllTextAsync(Out("scan.pdf")));
    }

    [Theory]
    [InlineData("/img/s/0/thumb")]
    [InlineData("/export/nope/abc.pdf")]
    [InlineData("export")]
    public void Anything_but_a_live_export_path_is_refused(string path) =>
        Assert.Throws<BridgeException>(() => _api.SaveExport(path, Out("x.pdf"), overwrite: true));

    [Fact]
    public void Its_methods_are_callable_over_the_bridge()
    {
        var bridge = new BridgeDispatcher(_desktop.Api, _ => { }, NullLogger.Instance, [_api], CancellationToken.None);
        Assert.Contains(nameof(DesktopApi.PickSavePath), bridge.Methods);
        Assert.Contains(nameof(DesktopApi.PickFolder), bridge.Methods);
        Assert.Contains(nameof(DesktopApi.SaveExport), bridge.Methods);
    }

    private sealed class FakeDialogs : IDesktopDialogs
    {
        public string? SaveAnswer { get; set; }

        public string? FolderAnswer { get; set; }

        public string LastSuggested { get; private set; } = "";

        public string LastExtension { get; private set; } = "";

        public Task<string?> SaveFileAsync(string suggestedPath, string extension)
        {
            LastSuggested = suggestedPath;
            LastExtension = extension;
            return Task.FromResult(SaveAnswer);
        }

        public Task<string?> PickFolderAsync(string startFolder) => Task.FromResult(FolderAnswer);
    }
}
