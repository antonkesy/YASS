using System.Collections.ObjectModel;
using Microsoft.AspNetCore.SignalR.Client;
using Yawss.Core.Domain;
using Yawss.Core.Hub;

namespace Yawss.Server.Tests.Hub;

/// <summary>Typed wrapper over a connection that also records every server push.</summary>
public sealed class HubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    private HubClient(HubConnection connection)
    {
        _connection = connection;
        connection.On<ScannerStatusDto>(nameof(IYawssClient.ScannerStatusChanged), s => StatusChanges.Add(s));
        connection.On<SessionDto>(nameof(IYawssClient.SessionChanged), s => SessionChanges.Add(s));
        connection.On<string, PageDto>(nameof(IYawssClient.PageAdded), (id, p) => PagesAdded.Add((id, p)));
        connection.On<string, PageDto>(nameof(IYawssClient.PageUpdated), (id, p) => PagesUpdated.Add((id, p)));
        connection.On<string, int>(nameof(IYawssClient.ExportProgress), (id, p) => Progress.Add((id, p)));
    }

    public Collection<ScannerStatusDto> StatusChanges { get; } = [];

    public Collection<SessionDto> SessionChanges { get; } = [];

    public Collection<(string SessionId, PageDto Page)> PagesAdded { get; } = [];

    public Collection<(string SessionId, PageDto Page)> PagesUpdated { get; } = [];

    public Collection<(string SessionId, int Percent)> Progress { get; } = [];

    public static async Task<HubClient> ConnectAsync(YawssFactory factory)
    {
        var client = new HubClient(factory.CreateHub());
        await client._connection.StartAsync();
        return client;
    }

    public Task<SessionDto> StartScan(ScanOptionsDto? requested = null) => _connection.InvokeAsync<SessionDto>(nameof(IYawssHub.StartScan), requested);

    public Task StopScan(string id) => _connection.InvokeAsync(nameof(IYawssHub.StopScan), id);

    public Task<SessionDto> GetSession(string id) => _connection.InvokeAsync<SessionDto>(nameof(IYawssHub.GetSession), id);

    public Task<SessionDto[]> ListSessions() => _connection.InvokeAsync<SessionDto[]>(nameof(IYawssHub.ListSessions));

    public Task DeleteSession(string id) => _connection.InvokeAsync(nameof(IYawssHub.DeleteSession), id);

    public Task<SessionDto> ApplyEdits(string id, params PageEdit[] edits) => _connection.InvokeAsync<SessionDto>(nameof(IYawssHub.ApplyEdits), id, edits);

    public Task<ExportResult> Export(string id, ExportMode mode) => _connection.InvokeAsync<ExportResult>(nameof(IYawssHub.Export), id, new ExportRequest(mode));

    public Task<ScannerStatusDto> GetScannerStatus() => _connection.InvokeAsync<ScannerStatusDto>(nameof(IYawssHub.GetScannerStatus));

    public Task<ScanOptionsDto> GetEffectiveOptions() => _connection.InvokeAsync<ScanOptionsDto>(nameof(IYawssHub.GetEffectiveOptions));

    public Task ReloadConfig() => _connection.InvokeAsync(nameof(IYawssHub.ReloadConfig));

    /// <summary>Starts a scan and waits for the session to leave <see cref="SessionState.Scanning"/>.</summary>
    public async Task<SessionDto> ScanToEnd(ScanOptionsDto? requested = null, TimeSpan? timeout = null)
    {
        SessionDto started = await StartScan(requested);
        return await WaitFor(started.Id, s => s.State != SessionState.Scanning, timeout);
    }

    public async Task<SessionDto> WaitFor(string id, Func<SessionDto, bool> predicate, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            SessionDto session = await GetSession(id);
            if (predicate(session))
            {
                return session;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Session {id} did not reach the expected state; last state {session.State} with {session.Pages.Length} pages.");
            }

            await Task.Delay(25);
        }
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
