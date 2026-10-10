using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Yass.App.Config;
using Yass.App.Sessions;
using Yass.Core.Config;
using Yass.Core.Domain;

namespace Yass.Server.Tests.Sessions;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yass-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Concurrent_updates_all_land_and_disk_matches_memory()
    {
        SessionStore store = NewStore();
        store.Create(new Session("s1", DateTimeOffset.UnixEpoch, SessionState.Review, [], new ScanOptions()) { Name = "0" });

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            store.Update("s1", s => s with { Name = (int.Parse(s.Name!, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) }))));

        Assert.Equal("200", store.Get("s1").Name);
        Assert.Equal("200", NewStore().Get("s1").Name);
    }

    [Fact]
    public void A_throwing_update_changes_nothing()
    {
        SessionStore store = NewStore();
        store.Create(new Session("s1", DateTimeOffset.UnixEpoch, SessionState.Review, [], new ScanOptions()));

        Assert.Throws<InvalidOperationException>(() => store.Update("s1", _ => throw new InvalidOperationException()));

        Assert.Null(store.Get("s1").Name);
    }

    private SessionStore NewStore()
    {
        var config = new YassConfig() with { Sessions = new SessionsConfig { Directory = _root } };
        return new SessionStore(new ConfigStore(config, Path.Combine(_root, "yass.yaml"), NullLogger<ConfigStore>.Instance), NullLogger<SessionStore>.Instance);
    }
}
