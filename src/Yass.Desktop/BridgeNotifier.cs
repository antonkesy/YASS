using System.Text.Json;
using Yass.App.Api;
using Yass.Core.Hub;

namespace Yass.Desktop;

/// <summary>Events before <see cref="Attach"/> are dropped: the client loads the whole state once it starts.</summary>
public sealed class BridgeNotifier : IClientNotifier, IYassClient
{
    private volatile Action<string>? _send;

    public IYassClient All => this;

    public void Attach(Action<string> send) => _send = send;

    public Task ScannerStatusChanged(ScannerStatusDto status) => Post(nameof(ScannerStatusChanged), status);

    public Task SessionChanged(SessionDto session) => Post(nameof(SessionChanged), session);

    public Task PageAdded(string sessionId, PageDto page) => Post(nameof(PageAdded), sessionId, page);

    public Task PageUpdated(string sessionId, PageDto page) => Post(nameof(PageUpdated), sessionId, page);

    public Task ExportProgress(string sessionId, int percent) => Post(nameof(ExportProgress), sessionId, percent);

    private Task Post(string name, params object?[] args)
    {
        _send?.Invoke(JsonSerializer.Serialize(new EventMessage(name, args), BridgeJson.Options));
        return Task.CompletedTask;
    }

    private sealed record EventMessage(
        [property: System.Text.Json.Serialization.JsonPropertyName("event")] string Event,
        [property: System.Text.Json.Serialization.JsonPropertyName("args")] object?[] Args);
}
