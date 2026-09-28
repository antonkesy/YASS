using TypedSignalR.Client;
using Yass.Core.Domain;

namespace Yass.Core.Hub;

/// <summary>Client → server. Served at <c>/hub</c> over MessagePack.</summary>
[Hub]
public interface IYassHub
{
    /// <summary>Starts a scan with the effective options, overridden by any editable keys in <paramref name="requested"/>.</summary>
    Task<SessionDto> StartScan(ScanOptionsDto? requested);

    Task StopScan(string sessionId);

    Task<SessionDto> GetSession(string sessionId);

    Task<SessionDto[]> ListSessions();

    /// <summary>Removes the session and its directory outright.</summary>
    Task DeleteSession(string sessionId);

    /// <summary>Renames a session. An empty or whitespace name clears it back to the timestamp.</summary>
    Task<SessionDto> RenameSession(string sessionId, string? name);

    /// <summary>
    /// Marks a session discarded but keeps it listed, unlike <see cref="DeleteSession"/>.
    /// Its pages are removed, since the point of discarding is to stop storing them.
    /// </summary>
    Task<SessionDto> DiscardSession(string sessionId);

    /// <summary>Deletes every session that is not currently scanning or exporting. Returns how many went.</summary>
    Task<int> ClearSessions();

    Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits);

    /// <summary>Scans one sheet and puts it in place of <paramref name="pageIndex"/>, keeping that page's edits.</summary>
    Task<SessionDto> RescanPage(string sessionId, int pageIndex);

    Task<ExportResult> Export(string sessionId, ExportRequest request);

    Task<ScannerStatusDto> GetScannerStatus();

    Task<ScanOptionsDto> GetEffectiveOptions();

    /// <summary>Re-reads every section of the config file except <c>server</c>.</summary>
    Task ReloadConfig();
}
