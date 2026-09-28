using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Yass.Core.Hub;
using Yass.Core.Scanning;
using Yass.Server.Config;
using Yass.Server.Hub;

namespace Yass.Server.Scanning;

/// <summary>Polls <see cref="IScannerBackend.GetStatusAsync"/> and pushes <c>ScannerStatusChanged</c> when the answer changes.</summary>
public sealed class ScannerStatusMonitor(
    IScannerBackend backend,
    IHubContext<YassHub, IYassClient> hub,
    ConfigStore config,
    TimeProvider time,
    ILogger<ScannerStatusMonitor> logger) : BackgroundService
{
    private ScannerStatus _last = new(ScannerState.Unknown, null, "Not checked yet.");
    private ScannerStatusDto _current = new(ScannerState.Unknown, null, "Not checked yet.", DateTimeOffset.UnixEpoch.ToString("O"));

    public ScannerStatusDto Current => Volatile.Read(ref _current);

    /// <summary>Forces a probe now, e.g. after a scan finished.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken) => ProbeAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProbeAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(config.Current.Scanner.PollIntervalSeconds), time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        ScannerStatus status;
        try
        {
            status = await backend.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
#pragma warning disable CA1031 // The monitor must survive any driver failure and report it instead.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Scanner status probe failed");
            status = new ScannerStatus(ScannerState.Error, null, ex.Message);
        }

        if (status == _last)
        {
            return;
        }

        _last = status;
        var dto = new ScannerStatusDto(status.State, status.DeviceName, status.Message, time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        Volatile.Write(ref _current, dto);
        logger.LogInformation("Scanner status: {State} {Device} {Message}", dto.State, dto.DeviceName, dto.Message);
        await hub.Clients.All.ScannerStatusChanged(dto).ConfigureAwait(false);
    }
}
