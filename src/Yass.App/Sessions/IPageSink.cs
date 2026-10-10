using Yass.Core.Domain;

namespace Yass.App.Sessions;

public interface IPageSink
{
    void Enqueue(Session session, Page page);
}
