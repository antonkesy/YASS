using Yass.Core.Domain;

namespace Yass.App.Sessions;

public interface IThumbnailStatus
{
    bool IsReady(Session session, Page page);
}
