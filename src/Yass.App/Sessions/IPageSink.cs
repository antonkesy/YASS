using Yass.Core.Domain;

namespace Yass.App.Sessions;

/// <summary>Receives pages whose derived images need (re)rendering, so it can happen in the background.</summary>
public interface IPageSink
{
    void Enqueue(Session session, Page page);
}
