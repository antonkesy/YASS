using Microsoft.AspNetCore.SignalR;
using Yawss.Core.Domain;
using Yawss.Core.Hub;
using Yawss.Server.Config;
using Yawss.Server.Scanning;

namespace Yawss.Server.Hub;

/// <summary>The single SignalR hub. Thin: every method delegates to a service and never blocks on scanning or rendering.</summary>
public sealed class YawssHub(ScannerStatusMonitor scanner, EffectiveOptions options, ConfigStore config, ILogger<YawssHub> logger)
    : Hub<IYawssClient>, IYawssHub
{
    public Task<SessionDto> StartScan(ScanOptionsDto? requested) => throw NotYet();

    public Task StopScan(string sessionId) => throw NotYet();

    public Task<SessionDto> GetSession(string sessionId) => throw NotYet();

    public Task<SessionDto[]> ListSessions() => throw NotYet();

    public Task DeleteSession(string sessionId) => throw NotYet();

    public Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits) => throw NotYet();

    public Task<ExportResult> Export(string sessionId, ExportRequest request) => throw NotYet();

    public Task<ScannerStatusDto> GetScannerStatus() => Task.FromResult(scanner.Current);

    public Task<ScanOptionsDto> GetEffectiveOptions() => Task.FromResult(options.Current());

    public Task ReloadConfig()
    {
        try
        {
            config.Reload();
            return Task.CompletedTask;
        }
        catch (Yawss.Core.Config.ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new HubException(ex.Message);
        }
    }

    private static HubException NotYet() => new("Not available yet.");
}
