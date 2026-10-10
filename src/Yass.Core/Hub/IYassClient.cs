using TypedSignalR.Client;

namespace Yass.Core.Hub;

[Receiver]
public interface IYassClient
{
    Task ScannerStatusChanged(ScannerStatusDto status);

    Task SessionChanged(SessionDto session);

    Task PageAdded(string sessionId, PageDto page);

    Task PageUpdated(string sessionId, PageDto page);

    Task ExportProgress(string sessionId, int percent);
}
