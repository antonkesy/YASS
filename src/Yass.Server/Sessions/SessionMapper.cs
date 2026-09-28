using System.Globalization;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Server.Scanning;

namespace Yass.Server.Sessions;

public sealed class SessionMapper(IThumbnailStatus thumbnails)
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
            session.Error);
    }

    public PageDto ToDto(Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PageDto(page.Index, page.Rotation, page.ColorOverride, page.Deleted, page.IsBlank, page.Hash, thumbnails.IsReady(session, page));
    }
}
