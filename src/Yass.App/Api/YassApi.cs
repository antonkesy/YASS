using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Scanning;
using Yass.App.Sessions;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Core.Scanning;

namespace Yass.App.Api;

/// <summary>
/// Everything <see cref="IYassHub"/> promises, with no transport attached. Each method mirrors the
/// contract; those whose work can be abandoned also take the caller's cancellation. The SignalR hub and the
/// desktop bridge both delegate here, so they behave identically. Errors meant for the user are
/// thrown as <see cref="YassException"/>.
/// </summary>
public sealed class YassApi(
    ScannerStatusMonitor scanner,
    IScannerBackend backend,
    EffectiveOptions options,
    ConfigStore config,
    SessionStore sessions,
    SessionMapper mapper,
    ScanService scans,
    EditService editor,
    AutoCropService cropper,
    MergeService merger,
    ExportService exporter,
    IClientNotifier clients,
    TimeProvider time,
    ILogger<YassApi> logger)
{
    public async Task<SessionDto> StartScan(ScanOptionsDto? requested)
    {
        SessionDto dto = mapper.ToDto(Guard(() => scans.Start(options.Resolve(requested))));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
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
            throw new YassException("Stop the scan before deleting the session.");
        }

        if (session.State == SessionState.Exporting)
        {
            // Deleting the directory under the writer would only turn the export's error into a worse one.
            throw new YassException("Wait for the export to finish before deleting the session.");
        }

        sessions.Delete(sessionId);
        await clients.All.SessionChanged(mapper.Removed(session, SessionMapper.Deleted)).ConfigureAwait(false);
    }

    public async Task<SessionDto> RenameSession(string sessionId, string? name)
    {
        Find(sessionId);
        string? trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is { Length: > 200 })
        {
            throw new YassException("Name is too long (200 characters maximum).");
        }

        SessionDto dto = mapper.ToDto(Guard(() => sessions.Update(sessionId, s => s with { Name = trimmed })));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> DiscardSession(string sessionId)
    {
        Find(sessionId);
        var discarded = new SessionEvent(SessionEventKind.Discarded, time.GetUtcNow());
        SessionDto dto = mapper.ToDto(Guard(() => sessions.Discard(sessionId, discarded)));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<int> ClearSessions()
    {
        Session[] removable = [.. sessions.List().Where(s => s.State is not (SessionState.Scanning or SessionState.Exporting))];
        foreach (Session session in removable)
        {
            sessions.Delete(session.Id);
            await clients.All.SessionChanged(mapper.Removed(session, SessionMapper.Deleted)).ConfigureAwait(false);
        }

        logger.LogInformation("Cleared {Count} session(s)", removable.Length);
        return removable.Length;
    }

    public async Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits)
    {
        Find(sessionId);
        return mapper.ToDto(await GuardAsync(() => editor.ApplyAsync(sessionId, edits ?? [])).ConfigureAwait(false));
    }

    public async Task<int> AutoCrop(string sessionId, int[]? targets, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        return await GuardAsync(() => cropper.CropAsync(sessionId, targets, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<SessionDto> RescanPage(string sessionId, int pageIndex, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        await GuardAsync(() => scans.RescanPageAsync(sessionId, pageIndex, cancellationToken)).ConfigureAwait(false);
        return mapper.ToDto(Find(sessionId));
    }

    public async Task<SessionDto> ScanMore(string sessionId)
    {
        Find(sessionId);
        SessionDto dto = mapper.ToDto(Guard(() => scans.StartMore(sessionId)));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> MergeSession(string targetId, string sourceId)
    {
        Find(targetId);
        Find(sourceId);
        SessionDto dto = mapper.ToDto(Guard(() => merger.Merge(targetId, sourceId)));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<ExportResult> Export(string sessionId, ExportRequest request, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        // The folder may exist but refuse this service: worth saying plainly that the export is what failed.
        return await GuardAsync(() => exporter.ExportAsync(sessionId, request ?? new ExportRequest(), cancellationToken), "Export failed: ").ConfigureAwait(false);
    }

    public Task<ScannerStatusDto> GetScannerStatus() => Task.FromResult(scanner.Current);

    public async Task<ScannerDeviceDto[]> ListDevices(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScannerDevice> devices = await backend.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        return [.. devices.Select(d => new ScannerDeviceDto(d.Id, d.Name, d.Driver, d.Connection))];
    }

    public Task<ScanOptionsDto> GetEffectiveOptions() => Task.FromResult(options.Current());

    public Task<string> GetServerFolder() =>
        Task.FromResult(config.Current.Output.SavesToServer() ? config.Current.Output.Directory : "");

    public Task ReloadConfig()
    {
        try
        {
            config.Reload();
            return Task.CompletedTask;
        }
        catch (ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new YassException(ex.Message, ex);
        }
    }

    private Session Find(string sessionId) =>
        SessionPaths.IsValidId(sessionId) && sessions.TryGet(sessionId, out Session session)
            ? session
            : throw new YassException($"Session {sessionId} not found.");

    /// <summary>
    /// The services say no with the BCL's exceptions — a state that forbids the call, an argument
    /// out of range, a session gone in the meantime, a disk that refused — and this is where those
    /// become a <see cref="YassException"/> the caller reads. Anything else is a bug and stays one.
    /// </summary>
    private static T Guard<T>(Func<T> work, string ioPrefix = "")
    {
        try
        {
            return work();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            throw new YassException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new YassException(ioPrefix + ex.Message, ex);
        }
    }

    private static async Task<T> GuardAsync<T>(Func<Task<T>> work, string ioPrefix = "")
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            throw new YassException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new YassException(ioPrefix + ex.Message, ex);
        }
    }
}
