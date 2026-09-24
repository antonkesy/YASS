using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Yawss.Core.Domain;
using Yawss.Core.Hub;
using Yawss.Server.Hub;
using Yawss.Server.Sessions;

namespace Yawss.Server.Rendering;

/// <summary>Background thumbnail generation. Bounded so a fast scanner cannot pile up decoded bitmaps; pushes <c>PageUpdated</c> when a thumbnail is ready.</summary>
public sealed class ThumbnailWorker(
    RenderCache cache,
    SessionStore store,
    SessionMapper mapper,
    IHubContext<YawssHub, IYawssClient> hub,
    ILogger<ThumbnailWorker> logger) : BackgroundService, IPageSink
{
    private readonly Channel<(string SessionId, Page Page)> _queue =
        Channel.CreateBounded<(string, Page)>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(session);
        _queue.Writer.TryWrite((session.Id, page));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach ((string sessionId, Page page) in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            if (!store.TryGet(sessionId, out Session session))
            {
                continue;
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
                continue;
            }

            // Re-read: the page may have been edited or reordered while rendering.
            if (store.TryGet(sessionId, out session))
            {
                Page? current = session.Pages.FirstOrDefault(p => p.SourceFile == page.SourceFile);
                if (current is not null)
                {
                    await hub.Clients.All.PageUpdated(sessionId, mapper.ToDto(session, current)).ConfigureAwait(false);
                }
            }
        }
    }
}
