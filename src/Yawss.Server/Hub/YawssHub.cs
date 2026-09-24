using Microsoft.AspNetCore.SignalR;
using Yawss.Core.Domain;
using Yawss.Core.Hub;
using Yawss.Server.Config;
using Yawss.Server.Scanning;
using Yawss.Server.Sessions;

namespace Yawss.Server.Hub;

/// <summary>The single SignalR hub. Thin: every method delegates to a service and never blocks on scanning or rendering.</summary>
public sealed class YawssHub(
    ScannerStatusMonitor scanner,
    EffectiveOptions options,
    ConfigStore config,
    SessionStore sessions,
    SessionMapper mapper,
    ScanService scans,
    EditService editor,
    ILogger<YawssHub> logger)
    : Hub<IYawssClient>, IYawssHub
{
    public async Task<SessionDto> StartScan(ScanOptionsDto? requested)
    {
        Session session;
        try
        {
            session = scans.Start(options.Resolve(requested));
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        SessionDto dto = mapper.ToDto(session);
        await Clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public Task StopScan(string sessionId)
    {
        scans.Stop(sessionId);
        return Task.CompletedTask;
    }

    public Task<SessionDto> GetSession(string sessionId) => Task.FromResult(mapper.ToDto(Find(sessionId)));

    public Task<SessionDto[]> ListSessions() => Task.FromResult(sessions.List().Select(mapper.ToDto).ToArray());

    public async Task DeleteSession(string sessionId)
    {
        Session session = Find(sessionId);
        if (session.State == SessionState.Scanning)
        {
            throw new HubException("Stop the scan before deleting the session.");
        }

        sessions.Delete(sessionId);
        await Clients.All.SessionChanged(mapper.ToDto(session with { State = SessionState.Done, Pages = [], Error = "deleted" })).ConfigureAwait(false);
    }

    public async Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits)
    {
        Find(sessionId);
        try
        {
            return mapper.ToDto(await editor.ApplyAsync(sessionId, edits ?? []).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new HubException(ex.Message);
        }
    }

    public Task<ExportResult> Export(string sessionId, ExportRequest request) => throw NotYet();

    public Task<ScannerStatusDto> GetScannerStatus() => Task.FromResult(scanner.Current);

    public Task<ScanOptionsDto> GetEffectiveOptions() => Task.FromResult(options.Current());

    public Task ReloadConfig()
    {
        try
        {
            config.Reload();
            return Task.CompletedTask;
        }
        catch (Yawss.Core.Config.ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new HubException(ex.Message);
        }
    }

    private Session Find(string sessionId) =>
        SessionPaths.IsValidId(sessionId) && sessions.TryGet(sessionId, out Session session)
            ? session
            : throw new HubException($"Session {sessionId} not found.");

    private static HubException NotYet() => new("Not available yet.");
}
