using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Scanning;
using Yass.App.Sessions;
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
    ExportService exporter,
    IPageSink sinkFor,
    IClientNotifier clients,
    TimeProvider time,
    ILogger<YassApi> logger)
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
            throw new YassException(ex.Message);
        }

        SessionDto dto = mapper.ToDto(session);
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
        await clients.All.SessionChanged(mapper.ToDto(session with { State = SessionState.Done, Pages = [], Error = "deleted" })).ConfigureAwait(false);
    }

    public async Task<SessionDto> RenameSession(string sessionId, string? name)
    {
        Find(sessionId);
        string? trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is { Length: > 200 })
        {
            throw new YassException("Name is too long (200 characters maximum).");
        }

        SessionDto dto = mapper.ToDto(sessions.Update(sessionId, s => s with { Name = trimmed }));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> DiscardSession(string sessionId)
    {
        Session session = Find(sessionId);
        if (session.State is SessionState.Scanning or SessionState.Exporting)
        {
            throw new YassException($"Session is {session.State}; wait for it to finish before discarding.");
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
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
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
            await clients.All.SessionChanged(mapper.ToDto(session with { State = SessionState.Done, Pages = [], Error = "deleted" })).ConfigureAwait(false);
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
            throw new YassException(ex.Message);
        }
    }

    public async Task<int> AutoCrop(string sessionId, int[]? targets, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        try
        {
            return await cropper.CropAsync(sessionId, targets, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new YassException(ex.Message);
        }
    }

    public async Task<SessionDto> RescanPage(string sessionId, int pageIndex, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        try
        {
            await scans.RescanPageAsync(sessionId, pageIndex, cancellationToken).ConfigureAwait(false);
            return mapper.ToDto(Find(sessionId));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            throw new YassException(ex.Message);
        }
    }

    public async Task<SessionDto> ScanMore(string sessionId)
    {
        Find(sessionId);
        try
        {
            SessionDto dto = mapper.ToDto(scans.StartMore(sessionId));
            await clients.All.SessionChanged(dto).ConfigureAwait(false);
            return dto;
        }
        catch (InvalidOperationException ex)
        {
            throw new YassException(ex.Message);
        }
    }

    public async Task<SessionDto> MergeSession(string targetId, string sourceId)
    {
        Session target = Find(targetId);
        Session source = Find(sourceId);

        if (target.State is not (SessionState.Review or SessionState.Done))
        {
            throw new YassException($"Session is {target.State}; pages can only be added in Review.");
        }

        if (string.Equals(targetId, sourceId, StringComparison.Ordinal))
        {
            throw new YassException("A scan cannot be merged into itself.");
        }

        Page[] incoming = [.. source.ActivePages];
        if (incoming.Length == 0)
        {
            throw new YassException("That scan has no pages left to add.");
        }

        // Page files are copied, not moved: the source scan stays intact and usable.
        string targetDir = SessionPaths.Directory(sessions.Root, targetId);
        var copied = new List<Page>();
        int next = target.Pages.Count;
        foreach (Page page in incoming)
        {
            string fileName = $"merged-{Guid.NewGuid():N}{Path.GetExtension(page.SourceFile)}";
            File.Copy(SessionPaths.Source(sessions.Root, source, page), Path.Combine(targetDir, fileName), overwrite: false);
            copied.Add(page with
            {
                Index = next++,
                SourceFile = fileName,
                // The merged run becomes its own document, which is the point of merging
                // rather than rescanning into this session.
                StartsDocument = copied.Count == 0,
                Deleted = false,
            });
        }

        Session merged = sessions.Update(targetId, s => s with
        {
            Pages = [.. s.Pages, .. copied.Select(p => PageHash.Rehash(p, s.OptionsUsed.Color))],
        });

        foreach (Page page in merged.Pages.Skip(target.Pages.Count))
        {
            sinkFor.Enqueue(merged, page);
        }

        SessionDto dto = mapper.ToDto(merged);
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        logger.LogInformation("Merged {Count} page(s) from {Source} into {Target}", copied.Count, sourceId, targetId);
        return dto;
    }

    public async Task<ExportResult> Export(string sessionId, ExportRequest request, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        try
        {
            return await exporter.ExportAsync(sessionId, request ?? new ExportRequest(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new YassException(ex.Message);
        }
        catch (IOException ex)
        {
            throw new YassException("Export failed: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            // The server folder exists but this service may not write there: worth saying plainly.
            throw new YassException("Export failed: " + ex.Message);
        }
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
        catch (Yass.Core.Config.ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new YassException(ex.Message);
        }
    }

    private Session Find(string sessionId) =>
        SessionPaths.IsValidId(sessionId) && sessions.TryGet(sessionId, out Session session)
            ? session
            : throw new YassException($"Session {sessionId} not found.");

}
