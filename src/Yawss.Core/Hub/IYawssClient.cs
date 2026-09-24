using TypedSignalR.Client;

namespace Yawss.Core.Hub;

/// <summary>Server → client pushes.</summary>
[Receiver]
public interface IYawssClient
{
    Task ScannerStatusChanged(ScannerStatusDto status);

    /// <summary>State transitions and whole-session changes such as reorder or delete.</summary>
    Task SessionChanged(SessionDto session);

    /// <summary>Emitted as soon as the page is on disk, before its thumbnail exists.</summary>
    Task PageAdded(string sessionId, PageDto page);

    /// <summary>After an edit or when a thumbnail becomes ready; carries the new hash.</summary>
    Task PageUpdated(string sessionId, PageDto page);

    Task ExportProgress(string sessionId, int percent);
}
