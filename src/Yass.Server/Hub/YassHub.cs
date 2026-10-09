using Microsoft.AspNetCore.SignalR;
using Yass.App.Api;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.Server.Hub;

/// <summary>
/// The single SignalR hub. Pure delegation to <see cref="YassApi"/>; <see cref="YassErrorFilter"/>
/// turns its errors into hub errors. The connection's abort token cancels long work.
/// </summary>
public sealed class YassHub(YassApi api) : Hub<IYassClient>, IYassHub
{
    public Task<SessionDto> StartScan(ScanOptionsDto? requested) => api.StartScan(requested);

    public Task StopScan(string sessionId) => api.StopScan(sessionId);

    public Task<SessionDto> GetSession(string sessionId) => api.GetSession(sessionId);

    public Task<SessionDto[]> ListSessions() => api.ListSessions();

    public Task DeleteSession(string sessionId) => api.DeleteSession(sessionId);

    public Task<SessionDto> RenameSession(string sessionId, string? name) => api.RenameSession(sessionId, name);

    public Task<SessionDto> DiscardSession(string sessionId) => api.DiscardSession(sessionId);

    public Task<int> ClearSessions() => api.ClearSessions();

    public Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits) => api.ApplyEdits(sessionId, edits);

    public Task<int> AutoCrop(string sessionId, int[]? targets) => api.AutoCrop(sessionId, targets, Context.ConnectionAborted);

    public Task<SessionDto> RescanPage(string sessionId, int pageIndex) => api.RescanPage(sessionId, pageIndex, Context.ConnectionAborted);

    public Task<SessionDto> ScanMore(string sessionId) => api.ScanMore(sessionId);

    public Task<SessionDto> MergeSession(string targetId, string sourceId) => api.MergeSession(targetId, sourceId);

    public Task<ExportResult> Export(string sessionId, ExportRequest request) => api.Export(sessionId, request, Context.ConnectionAborted);

    public Task<ScannerStatusDto> GetScannerStatus() => api.GetScannerStatus();

    public Task<ScannerDeviceDto[]> ListDevices() => api.ListDevices(Context.ConnectionAborted);

    public Task<ScanOptionsDto> GetEffectiveOptions() => api.GetEffectiveOptions();

    public Task<string> GetServerFolder() => api.GetServerFolder();

    public Task ReloadConfig() => api.ReloadConfig();
}
