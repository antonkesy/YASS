using System.Threading.Channels;
using Yass.App.Api;
using Yass.App.Sessions;
using Yass.Core.Domain;

namespace Yass.App.Rendering;

/// <summary>Bounded so a fast scanner cannot pile up decoded bitmaps.</summary>
public sealed class ThumbnailWorker(
    RenderCache cache,
    SessionStore store,
    SessionMapper mapper,
    IClientNotifier hub,
    RenderAhead ahead,
    ILogger<ThumbnailWorker> logger) : BackgroundService, IPageSink
{
    private readonly Channel<(string SessionId, Page Page)> _queue =
        Channel.CreateBounded<(string, Page)>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly Channel<(string SessionId, Page Page)> _previews =
        Channel.CreateBounded<(string, Page)>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(session);
        _queue.Writer.TryWrite((session.Id, page));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reuse the pending wait: an abandoned wait on a channel nobody writes to (previews, on the
        // server) stays queued, and one would pile up per idle spell.
        Task<bool>? thumbs = null;
        Task<bool>? previews = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            // Thumbnails first: a preview is only rendered while no thumbnail is waiting.
            if (_queue.Reader.TryRead(out (string SessionId, Page Page) next))
            {
                await ThumbnailAsync(next.SessionId, next.Page, stoppingToken).ConfigureAwait(false);
            }
            else if (_previews.Reader.TryRead(out (string SessionId, Page Page) preview))
            {
                await PreviewAsync(preview.SessionId, preview.Page, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                thumbs ??= _queue.Reader.WaitToReadAsync(stoppingToken).AsTask();
                if (ahead.Previews)
                {
                    previews ??= _previews.Reader.WaitToReadAsync(stoppingToken).AsTask();
                }

                await (previews is null ? thumbs : (Task)Task.WhenAny(thumbs, previews))
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                thumbs = thumbs.IsCompleted ? null : thumbs;
                previews = previews?.IsCompleted == true ? null : previews;
            }
        }
    }

    private async Task ThumbnailAsync(string sessionId, Page page, CancellationToken stoppingToken)
    {
        if (!store.TryGet(sessionId, out Session session))
        {
            return;
        }

        try
        {
            await cache.GetOrRenderAsync(session, page, ImageKind.Thumb, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
#pragma warning disable CA1031 // A bad page must not stop thumbnails for every later page.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Thumbnail for {Session}/{Page} failed", sessionId, page.Index);
            return;
        }

        // Re-read: the page may have been edited or reordered while rendering.
        if (store.TryGet(sessionId, out session))
        {
            Page? current = session.Pages.FirstOrDefault(p => p.SourceFile == page.SourceFile);
            if (current is not null)
            {
                await hub.All.PageUpdated(sessionId, mapper.ToDto(session, current)).ConfigureAwait(false);
                if (ahead.Previews)
                {
                    _previews.Writer.TryWrite((sessionId, current));
                }
            }
        }
    }

    private async Task PreviewAsync(string sessionId, Page page, CancellationToken stoppingToken)
    {
        // An edit since queueing changed the hash; the old one is not worth drawing.
        if (!store.TryGet(sessionId, out Session session) || session.Pages.FirstOrDefault(p => p.SourceFile == page.SourceFile) is not { } current)
        {
            return;
        }

        try
        {
            await cache.GetOrRenderAsync(session, current, ImageKind.Preview, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
#pragma warning disable CA1031 // Ahead of need only: whoever opens the page renders it then.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogDebug(ex, "Preview ahead for {Session}/{Page} failed", sessionId, current.Index);
        }
    }
}
