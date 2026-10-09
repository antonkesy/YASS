using System.Collections.Concurrent;
using Yass.Core.Domain;
using Yass.Core.Scanning;
using Yass.Server.Hub;
using Yass.Server.Scanning;

namespace Yass.Server.Sessions;

/// <summary>
/// Runs the scan loop off the hub thread: for each page from the backend, write it to disk, add it to the session,
/// push <c>PageAdded</c>, and hand it to the <see cref="IPageSink"/>. Ends in <see cref="SessionState.Review"/>
/// when the feeder is empty or the scan is stopped, or <see cref="SessionState.Failed"/> on a driver error.
/// </summary>
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
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);

    public bool IsScanning => !_running.IsEmpty;

    /// <exception cref="InvalidOperationException">A scan is already running.</exception>
    public Session Start(ScanOptions options)
    {
        var session = new Session(SessionPaths.NewId(time.GetUtcNow()), time.GetUtcNow(), SessionState.Scanning, [], options);
        var cts = new CancellationTokenSource();
        if (!_running.IsEmpty || !_running.TryAdd(session.Id, cts))
        {
            cts.Dispose();
            throw new InvalidOperationException("A scan is already running.");
        }

        store.Create(session);
        _ = Task.Run(() => RunAsync(session.Id, options, 0, cts.Token), CancellationToken.None);
        return session;
    }

    /// <summary>
    /// Runs the feeder again and appends what comes out to an existing session, with the
    /// options that session was scanned with, so added pages match the ones already there.
    /// </summary>
    /// <exception cref="InvalidOperationException">A scan is running or the session is not editable.</exception>
    public Session StartMore(string sessionId)
    {
        Session session = store.Get(sessionId);
        if (session.State is not (SessionState.Review or SessionState.Done))
        {
            throw new InvalidOperationException($"Session is {session.State}; pages can only be added in Review.");
        }

        var cts = new CancellationTokenSource();
        if (!_running.IsEmpty || !_running.TryAdd(sessionId, cts))
        {
            cts.Dispose();
            throw new InvalidOperationException("A scan is already running.");
        }

        Session scanning = store.Update(sessionId, s => s with { State = SessionState.Scanning, Error = null });

        // The new run numbers its pages from 1 again, so page files are offset past the ones
        // already on disk; without it the second run would overwrite the first run's pages.
        int offset = scanning.Pages.Count;
        _ = Task.Run(() => RunAsync(sessionId, scanning.OptionsUsed, offset, cts.Token), CancellationToken.None);
        return scanning;
    }

    /// <summary>
    /// Pulls one more sheet and puts it in place of an existing page, keeping that page's
    /// position and its edits. The old source file is replaced, so nothing else has to move.
    /// </summary>
    /// <exception cref="InvalidOperationException">A scan is running, the session is not editable, or the feeder was empty.</exception>
    public async Task<Page> RescanPageAsync(string sessionId, int pageIndex, CancellationToken cancellationToken)
    {
        if (!_running.IsEmpty)
        {
            throw new InvalidOperationException("A scan is already running.");
        }

        Session session = store.Get(sessionId);
        if (session.State is not (SessionState.Review or SessionState.Done))
        {
            throw new InvalidOperationException($"Session is {session.State}; pages can only be rescanned in Review.");
        }

        if (pageIndex < 0 || pageIndex >= session.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page {pageIndex} is not in this session.");
        }

        ScannedPage? sheet = null;
        await foreach (ScannedPage scanned in backend.ScanAsync(session.OptionsUsed, cancellationToken).ConfigureAwait(false))
        {
            sheet = scanned;
            break;
        }

        if (sheet is null)
        {
            throw new InvalidOperationException("Nothing came out of the scanner; is the page loaded?");
        }

        Page replaced = session.Pages[pageIndex];
        await File.WriteAllBytesAsync(
            Path.Combine(SessionPaths.Directory(store.Root, sessionId), replaced.SourceFile),
            sheet.Png,
            CancellationToken.None).ConfigureAwait(false);

        Page? updated = null;
        Session after = store.Update(sessionId, s =>
        {
            var pages = s.Pages.ToList();
            // The new sheet inherits the slot's rotation, filter and crop: the user is
            // replacing a bad scan of this page, not starting it over.
            updated = PageHash.Rehash(
                pages[pageIndex] with { SourceHash = PageHash.OfBytes(sheet.Png), IsBlank = sheet.IsBlank, Deleted = false },
                s.OptionsUsed.Color);
            pages[pageIndex] = updated;
            return s with { Pages = pages };
        });

        sink.Enqueue(after, updated!);
        await hub.All.PageUpdated(sessionId, mapper.ToDto(after, updated!)).ConfigureAwait(false);
        logger.LogInformation("Rescanned page {Page} of session {Session}", pageIndex, sessionId);
        return updated!;
    }

    public void Stop(string sessionId)
    {
        if (_running.TryGetValue(sessionId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
        }
    }

    /// <summary>Awaitable end of the scan for the given session; completes immediately when none is running.</summary>
    public Task WhenFinished(string sessionId, CancellationToken cancellationToken) =>
        _running.TryGetValue(sessionId, out CancellationTokenSource? cts)
            ? WaitAsync(cts.Token, cancellationToken)
            : Task.CompletedTask;

    private static async Task WaitAsync(CancellationToken scan, CancellationToken caller)
    {
        var tcs = new TaskCompletionSource();
        using CancellationTokenRegistration reg = scan.Register(() => tcs.TrySetResult());
        using CancellationTokenRegistration reg2 = caller.Register(() => tcs.TrySetCanceled(caller));
        await tcs.Task.ConfigureAwait(false);
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
            if (_running.TryRemove(sessionId, out CancellationTokenSource? cts))
            {
                await cts.CancelAsync().ConfigureAwait(false);
                cts.Dispose();
            }
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
