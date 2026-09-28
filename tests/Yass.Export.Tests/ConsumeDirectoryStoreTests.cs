using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Yass.Core.Config;
using Yass.Core.Export;

namespace Yass.Export.Tests;

public sealed class ConsumeDirectoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yass-store-tests", Guid.NewGuid().ToString("N"));

    private ConsumeDirectoryStore Store(string template = "{date:yyyyMMdd}_scan.pdf") =>
        new(() => new OutputConfig { Directory = _dir, FilenameTemplate = template }, NullLogger<ConsumeDirectoryStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task Final_file_appears_only_after_the_body_is_complete()
    {
        string[]? seenDuringWrite = null;

        string path = await Store().SaveAsync(async (stream, ct) =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("%PDF-1.4 part one "), ct);
            await stream.FlushAsync(ct);
            seenDuringWrite = Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray()!;
            await stream.WriteAsync(Encoding.ASCII.GetBytes("part two"), ct);
        }, new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None);

        Assert.NotNull(seenDuringWrite);
        string temp = Assert.Single(seenDuringWrite);
        Assert.StartsWith(ConsumeDirectoryStore.TempPrefix, temp, StringComparison.Ordinal);
        Assert.EndsWith(".tmp", temp, StringComparison.Ordinal);
        Assert.Equal(["scan"], Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!.Split('_').Last().Replace(".pdf", "", StringComparison.Ordinal)));
        Assert.EndsWith("_scan.pdf", path, StringComparison.Ordinal);
        Assert.Equal("%PDF-1.4 part one part two", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Failed_write_leaves_no_file_behind()
    {
        ConsumeDirectoryStore store = Store();

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync((_, _) => throw new IOException("scanner exploded"), new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Cancelled_write_leaves_no_file_behind()
    {
        using var cts = new CancellationTokenSource();
        ConsumeDirectoryStore store = Store();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(async (stream, ct) =>
        {
            await stream.WriteAsync(new byte[10], ct);
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
        }, new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), cts.Token));

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Existing_names_get_a_numeric_suffix()
    {
        ConsumeDirectoryStore store = Store("fixed.pdf");
        Func<Stream, CancellationToken, Task> body = (s, ct) => s.WriteAsync(new byte[] { 1 }, ct).AsTask();

        string first = await store.SaveAsync(body, new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None);
        string second = await store.SaveAsync(body, new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None);
        string third = await store.SaveAsync(body, new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None);

        Assert.Equal("fixed.pdf", Path.GetFileName(first));
        Assert.Equal("fixed-2.pdf", Path.GetFileName(second));
        Assert.Equal("fixed-3.pdf", Path.GetFileName(third));
        Assert.Equal(3, Directory.GetFiles(_dir).Length);
    }

    [Fact]
    public async Task Creates_the_directory_when_missing()
    {
        string nested = Path.Combine(_dir, "a", "b");
        var store = new ConsumeDirectoryStore(() => new OutputConfig { Directory = nested }, NullLogger<ConsumeDirectoryStore>.Instance);

        string path = await store.SaveAsync((s, ct) => s.WriteAsync(new byte[] { 1 }, ct).AsTask(), new DocumentTarget(DateTimeOffset.UtcNow, null, null, ".pdf"), CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.StartsWith(nested, path, StringComparison.Ordinal);
    }
}
