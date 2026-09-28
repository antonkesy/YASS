using System.Globalization;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Server.Scanning;

namespace Yass.Server.Sessions;

public sealed class SessionMapper(IThumbnailStatus thumbnails, SessionStore store)
{
    public SessionDto ToDto(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionDto(
            session.Id,
            session.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            session.State,
            session.Pages.Select(p => ToDto(session, p)).ToArray(),
            EffectiveOptions.ToDto(session.OptionsUsed),
            session.Error,
            session.Name,
            session.Events.Select(ToDto).ToArray(),
            SessionPaths.SizeOnDisk(store.Root, session.Id));
    }

    public PageDto ToDto(Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        CropBox? crop = page.Crop is { } c && !c.IsEmpty() ? c : null;
        return new PageDto(page.Index, page.Rotation, page.Filter, crop, page.StartsDocument, page.Deleted, page.IsBlank, page.Hash, thumbnails.IsReady(session, page));
    }

    private static SessionEventDto ToDto(SessionEvent @event) =>
        new(@event.Kind, @event.At.ToString("O", CultureInfo.InvariantCulture), @event.Path);
}
