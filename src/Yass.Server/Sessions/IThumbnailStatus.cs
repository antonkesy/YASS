using Yass.Core.Domain;

namespace Yass.Server.Sessions;

/// <summary>Whether a page's thumbnail has been rendered.</summary>
public interface IThumbnailStatus
{
    bool IsReady(Session session, Page page);
}
