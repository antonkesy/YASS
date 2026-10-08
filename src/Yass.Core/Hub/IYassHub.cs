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

    /// <summary>
    /// Crops the scanner's dark frame off the target pages (all of them when null or empty), as
    /// ordinary crop edits. Returns how many pages had one.
    /// </summary>
    Task<int> AutoCrop(string sessionId, int[]? targets);

    /// <summary>Scans one sheet and puts it in place of <paramref name="pageIndex"/>, keeping that page's edits.</summary>
    Task<SessionDto> RescanPage(string sessionId, int pageIndex);

    /// <summary>Runs the feeder again and appends the pages to an existing session.</summary>
    Task<SessionDto> ScanMore(string sessionId);

    /// <summary>
    /// Copies the active pages of <paramref name="sourceId"/> onto the end of
    /// <paramref name="targetId"/>, starting a new document at the join. The source is left alone.
    /// </summary>
    Task<SessionDto> MergeSession(string targetId, string sourceId);

    Task<ExportResult> Export(string sessionId, ExportRequest request);

    Task<ScannerStatusDto> GetScannerStatus();

    /// <summary>Every scanner the driver can see, so a browser can pick one to scan on.</summary>
    Task<ScannerDeviceDto[]> ListDevices();

    Task<ScanOptionsDto> GetEffectiveOptions();

    /// <summary>
    /// The server's <c>output.directory</c>, or empty when saving to the server is off. A browser
    /// only offers the option when this names a folder.
    /// </summary>
    Task<string> GetServerFolder();

    /// <summary>Re-reads every section of the config file except <c>server</c>.</summary>
    Task ReloadConfig();
}
