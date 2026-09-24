using Yawss.Core.Domain;

namespace Yawss.Server.Sessions;

/// <summary>Whether a page's thumbnail has been rendered.</summary>
public interface IThumbnailStatus
{
    bool IsReady(Session session, Page page);
}
