using Yawss.Core.Domain;

namespace Yawss.Server.Sessions;

/// <summary>Whether a page's thumbnail has been rendered. Replaced by the render cache once it exists.</summary>
public interface IThumbnailStatus
{
    bool IsReady(Session session, Page page);
}

internal sealed class NoThumbnails : IThumbnailStatus
{
    public bool IsReady(Session session, Page page) => false;
}
