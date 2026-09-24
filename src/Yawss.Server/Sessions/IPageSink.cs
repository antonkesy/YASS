using Yawss.Core.Domain;

namespace Yawss.Server.Sessions;

/// <summary>Notified when a page's source file is on disk, so derived images can be produced in the background.</summary>
public interface IPageSink
{
    void PageAdded(Session session, Page page);
}

internal sealed class NoPageSink : IPageSink
{
    public void PageAdded(Session session, Page page)
    {
    }
}
