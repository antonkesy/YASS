using Microsoft.AspNetCore.SignalR;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Server.Config;
using Yass.Server.Export;
using Yass.Server.Scanning;
using Yass.Server.Sessions;

namespace Yass.Server.Hub;

/// <summary>The single SignalR hub. Thin: every method delegates to a service and never blocks on scanning or rendering.</summary>
public sealed class YassHub(
    ScannerStatusMonitor scanner,
    EffectiveOptions options,
    ConfigStore config,
    SessionStore sessions,
    SessionMapper mapper,
    ScanService scans,
    EditService editor,
    ExportService exporter,
    TimeProvider time,
    ILogger<YassHub> logger)
    : Hub<IYassClient>, IYassHub
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

    public async Task<SessionDto> RenameSession(string sessionId, string? name)
    {
        Find(sessionId);
        string? trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is { Length: > 200 })
        {
            throw new HubException("Name is too long (200 characters maximum).");
        }

        SessionDto dto = mapper.ToDto(sessions.Update(sessionId, s => s with { Name = trimmed }));
        await Clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> DiscardSession(string sessionId)
    {
        Session session = Find(sessionId);
        if (session.State is SessionState.Scanning or SessionState.Exporting)
        {
            throw new HubException($"Session is {session.State}; wait for it to finish before discarding.");
        }

        foreach (Page page in session.Pages)
        {
            string file = SessionPaths.Source(sessions.Root, session, page);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        string cache = SessionPaths.Cache(sessions.Root, sessionId);
        if (Directory.Exists(cache))
        {
            Directory.Delete(cache, recursive: true);
        }

        SessionDto dto = mapper.ToDto(sessions.Update(
            sessionId,
            s => (s with { State = SessionState.Done, Pages = [] }).With(new SessionEvent(SessionEventKind.Discarded, time.GetUtcNow()))));
        await Clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<int> ClearSessions()
    {
        var removable = sessions.List()
            .Where(s => s.State is not (SessionState.Scanning or SessionState.Exporting))
            .ToArray();

        foreach (Session session in removable)
        {
            sessions.Delete(session.Id);
            await Clients.All.SessionChanged(mapper.ToDto(session with { State = SessionState.Done, Pages = [], Error = "deleted" })).ConfigureAwait(false);
        }

        logger.LogInformation("Cleared {Count} session(s)", removable.Length);
        return removable.Length;
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

    public async Task<SessionDto> RescanPage(string sessionId, int pageIndex)
    {
        Find(sessionId);
        try
        {
            await scans.RescanPageAsync(sessionId, pageIndex, Context.ConnectionAborted).ConfigureAwait(false);
            return mapper.ToDto(Find(sessionId));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task<ExportResult> Export(string sessionId, ExportRequest request)
    {
        Find(sessionId);
        try
        {
            return await exporter.ExportAsync(sessionId, request ?? new ExportRequest(ExportMode.Save), Context.ConnectionAborted).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (IOException ex)
        {
            throw new HubException("Export failed: " + ex.Message);
        }
    }

    public Task<ScannerStatusDto> GetScannerStatus() => Task.FromResult(scanner.Current);

    public Task<ScanOptionsDto> GetEffectiveOptions() => Task.FromResult(options.Current());

    public Task ReloadConfig()
    {
        try
        {
            config.Reload();
            return Task.CompletedTask;
        }
        catch (Yass.Core.Config.ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new HubException(ex.Message);
        }
    }

    private Session Find(string sessionId) =>
        SessionPaths.IsValidId(sessionId) && sessions.TryGet(sessionId, out Session session)
            ? session
            : throw new HubException($"Session {sessionId} not found.");

}
