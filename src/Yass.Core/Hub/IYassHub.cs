using TypedSignalR.Client;
using Yass.Core.Domain;

namespace Yass.Core.Hub;

[Hub]
public interface IYassHub
{
    Task<SessionDto> StartScan(ScanOptionsDto? requested);

    Task StopScan(string sessionId);

    Task<SessionDto> GetSession(string sessionId);

    Task<SessionDto[]> ListSessions();

    Task DeleteSession(string sessionId);

    /// <summary>Renames a session. An empty or whitespace name clears it back to the timestamp.</summary>
    Task<SessionDto> RenameSession(string sessionId, string? name);

    /// <summary>Unlike <see cref="DeleteSession"/>, keeps the session listed but removes its pages.</summary>
    Task<SessionDto> DiscardSession(string sessionId);

    /// <summary>Skips sessions that are scanning or exporting. Returns how many were deleted.</summary>
    Task<int> ClearSessions();

    Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits);

    /// <summary>Null or empty targets mean all pages. Returns how many pages had a frame to crop.</summary>
    Task<int> AutoCrop(string sessionId, int[]? targets);

    /// <summary>Replaces <paramref name="pageIndex"/> with one new sheet, keeping that page's edits.</summary>
    Task<SessionDto> RescanPage(string sessionId, int pageIndex);

    Task<SessionDto> ScanMore(string sessionId);

    /// <summary>Appends the active pages of <paramref name="sourceId"/>, starting a new document at the join. The source is left alone.</summary>
    Task<SessionDto> MergeSession(string targetId, string sourceId);

    Task<ExportResult> Export(string sessionId, ExportRequest request);

    Task<ScannerStatusDto> GetScannerStatus();

    Task<ScannerDeviceDto[]> ListDevices();

    /// <summary>Looks for scanners again now rather than from the cache, and re-checks the status.</summary>
    Task<ScannerDeviceDto[]> RefreshDevices();

    Task<ScanOptionsDto> GetEffectiveOptions();

    /// <summary>Empty when saving to the server is off.</summary>
    Task<string> GetServerFolder();

    /// <summary>Everything except the <c>server</c> section.</summary>
    Task ReloadConfig();
}
