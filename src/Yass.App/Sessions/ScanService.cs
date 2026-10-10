using Yass.App.Api;
using Yass.App.Scanning;
using Yass.Core.Domain;
using Yass.Core.Scanning;

namespace Yass.App.Sessions;

public sealed class ScanService(
    IScannerBackend backend,
    SessionStore store,
    SessionMapper mapper,
    IPageSink sink,
    IClientNotifier hub,
    ScannerStatusMonitor status,
    TimeProvider time,
    ILogger<ScanService> logger)
{
    // Claimed and released under the gate: a check and a separate claim would let two callers both see the scanner free.
    private readonly Lock _gate = new();
    private (string SessionId, CancellationTokenSource Cts)? _active;

    public Session Start(ScanOptions options)
    {
        DateTimeOffset now = time.GetUtcNow();
        var session = new Session(SessionPaths.NewId(now), now, SessionState.Scanning, [], options);
        CancellationTokenSource cts = Claim(session.Id);
        try
        {
            store.Create(session);
        }
        catch
        {
            Release(session.Id);
            throw;
        }

        _ = Task.Run(() => RunAsync(session.Id, options, 0, cts.Token), CancellationToken.None);
        return session;
    }

    /// <summary>Uses the session's original options so added pages match.</summary>
    public Session StartMore(string sessionId)
    {
        CancellationTokenSource cts = Claim(sessionId);
        Session scanning;
        try
        {
            scanning = store.Update(sessionId, s =>
            {
                EnsureEditable(s, "pages can only be added in Review");
                return s with { State = SessionState.Scanning, Error = null };
            });
        }
        catch
        {
            Release(sessionId);
            throw;
        }

        // The new run numbers pages from 1 again; offset so it does not overwrite the first run's files.
        int offset = scanning.Pages.Count;
        _ = Task.Run(() => RunAsync(sessionId, scanning.OptionsUsed, offset, cts.Token), CancellationToken.None);
        return scanning;
    }

    /// <summary>Replaces the old source file in place, keeping the page's position and edits.</summary>
    public async Task<Page> RescanPageAsync(string sessionId, int pageIndex, CancellationToken cancellationToken)
    {
        Session session = store.Get(sessionId);
        EnsureEditable(session, "pages can only be rescanned in Review");
        if (pageIndex < 0 || pageIndex >= session.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page {pageIndex} is not in this session.");
        }

        // Pages can be reordered meanwhile, so the page is followed by its file, not its position.
        Page replaced = session.Pages[pageIndex];
        CancellationTokenSource cts = Claim(sessionId, cancellationToken);
        ScannedPage? sheet = null;
        try
        {
            await foreach (ScannedPage scanned in backend.ScanAsync(session.OptionsUsed, cts.Token).ConfigureAwait(false))
            {
                sheet = scanned;
                break;
            }
        }
        finally
        {
            Release(sessionId);
        }

        if (sheet is null)
        {
            throw new InvalidOperationException("Nothing came out of the scanner; is the page loaded?");
        }

        // Written aside and renamed over the old file, so a render never sees half a page.
        string source = SessionPaths.Source(store.Root, session, replaced);
        await File.WriteAllBytesAsync(source + ".tmp", sheet.Png, CancellationToken.None).ConfigureAwait(false);
        File.Move(source + ".tmp", source, overwrite: true);

        Page? updated = null;
        Session after = store.Update(sessionId, s =>
        {
            EnsureEditable(s, "pages can only be rescanned in Review");
            var pages = s.Pages.ToList();
            int at = pages.FindIndex(p => p.SourceFile == replaced.SourceFile);
            if (at < 0)
            {
                throw new InvalidOperationException("That page is no longer in this session.");
            }

            updated = PageHash.Rehash(
                pages[at] with { SourceHash = PageHash.OfBytes(sheet.Png), IsBlank = sheet.IsBlank, Deleted = false },
                s.OptionsUsed.Color);
            pages[at] = updated;
            return s with { Pages = pages };
        });

        sink.Enqueue(after, updated!);
        await hub.All.PageUpdated(sessionId, mapper.ToDto(after, updated!)).ConfigureAwait(false);
        logger.LogInformation("Rescanned page {Page} of session {Session}", updated!.Index, sessionId);
        return updated;
    }

    public void Stop(string sessionId)
    {
        lock (_gate)
        {
            if (_active is { } active && active.SessionId == sessionId)
            {
                active.Cts.Cancel();
            }
        }
    }

    private static void EnsureEditable(Session session, string refusal)
    {
        if (session.State is not (SessionState.Review or SessionState.Done))
        {
            throw new InvalidOperationException($"Session is {session.State}; {refusal}.");
        }
    }

    private CancellationTokenSource Claim(string sessionId, CancellationToken link = default)
    {
        lock (_gate)
        {
            if (_active is not null)
            {
                throw new InvalidOperationException("A scan is already running.");
            }

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(link);
            _active = (sessionId, cts);
            return cts;
        }
    }

    private void Release(string sessionId)
    {
        CancellationTokenSource? cts = null;
        lock (_gate)
        {
            if (_active is { } active && active.SessionId == sessionId)
            {
                cts = active.Cts;
                _active = null;
            }
        }

        cts?.Dispose();
    }

    private async Task RunAsync(string sessionId, ScanOptions options, int fileNumberOffset, CancellationToken cancellationToken)
    {
        string dir = SessionPaths.Directory(store.Root, sessionId);
        string? error = null;
        try
        {
            await foreach (ScannedPage scanned in backend.ScanAsync(options, cancellationToken).ConfigureAwait(false))
            {
                string fileName = SessionPaths.PageFileName(fileNumberOffset + scanned.Number);
                await File.WriteAllBytesAsync(Path.Combine(dir, fileName), scanned.Png, CancellationToken.None).ConfigureAwait(false);

                string sourceHash = PageHash.OfBytes(scanned.Png);
                Page? added = null;
                Session session = store.Update(sessionId, s =>
                {
                    var page = new Page(s.Pages.Count, fileName, Rotation.R0, PageFilter.Original, null, false, scanned.IsBlank, scanned.IsBlank, sourceHash, "");
                    added = PageHash.Rehash(page, s.OptionsUsed.Color);
                    return s with { Pages = [.. s.Pages, added] };
                });

                await hub.All.PageAdded(sessionId, mapper.ToDto(session, added!)).ConfigureAwait(false);
                sink.Enqueue(session, added!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Scan {Session} stopped by user", sessionId);
        }
#pragma warning disable CA1031 // Any driver failure ends the session as Failed with its message.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Scan {Session} failed", sessionId);
            error = ex.Message;
        }
        finally
        {
            Release(sessionId);
        }

        Session finished = store.Update(sessionId, s => s with
        {
            State = error is null || s.Pages.Count > 0 ? SessionState.Review : SessionState.Failed,
            Error = error,
        });
        await hub.All.SessionChanged(mapper.ToDto(finished)).ConfigureAwait(false);
        await status.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
