using Yawss.Core.Domain;

namespace Yawss.Server.Sessions;

/// <summary>Receives pages whose derived images need (re)rendering, so it can happen in the background.</summary>
public interface IPageSink
{
    void Enqueue(Session session, Page page);
}

internal sealed class NoPageSink : IPageSink
{
    public void Enqueue(Session session, Page page)
    {
    }
}
