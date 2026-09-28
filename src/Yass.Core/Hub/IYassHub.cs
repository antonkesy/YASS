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

    Task DeleteSession(string sessionId);

    Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits);

    Task<ExportResult> Export(string sessionId, ExportRequest request);

    Task<ScannerStatusDto> GetScannerStatus();

    Task<ScanOptionsDto> GetEffectiveOptions();

    /// <summary>Re-reads every section of the config file except <c>server</c>.</summary>
    Task ReloadConfig();
}
