using Microsoft.AspNetCore.SignalR;
using Yass.Core.Hub;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

public sealed class ConfigReloadTests
{
    [Fact]
    public async Task Reload_picks_up_non_server_keys_and_keeps_listen()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        string path = Path.Combine(factory.Root, "yass.yaml");
        await File.WriteAllTextAsync(path, $"""
            server:
              listen: "127.0.0.1:1"
            scanner:
              defaults:
                dpi: 600
              ui_editable: [dpi] # deprecated, still accepted
            sessions:
              directory: {factory.Config.Sessions.Directory}
            """);

        await hub.ReloadConfig();

        ScanOptionsDto options = await hub.GetEffectiveOptions();
        Assert.Equal(600, options.Dpi);
    }

    [Fact]
    public async Task Invalid_file_is_rejected_and_previous_config_stays()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        await File.WriteAllTextAsync(Path.Combine(factory.Root, "yass.yaml"), "scanner:\n  defaults:\n    dpi: 1\n");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.ReloadConfig());

        Assert.Contains("scanner.defaults.dpi", ex.Message, StringComparison.Ordinal);
        Assert.Equal(300, (await hub.GetEffectiveOptions()).Dpi);
    }
}
