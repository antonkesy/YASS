using System.Globalization;
using Yass.App.Api;
using Yass.App.Config;
using Yass.Core.Hub;
using Yass.Core.Scanning;

namespace Yass.App.Scanning;

public sealed class ScannerStatusMonitor(
    IScannerBackend backend,
    IClientNotifier hub,
    ConfigStore config,
    TimeProvider time,
    ILogger<ScannerStatusMonitor> logger) : BackgroundService
{
    private ScannerStatus _last = new(ScannerState.Unknown, null, "Not checked yet.");
    private ScannerStatusDto _current = new(ScannerState.Unknown, null, "Not checked yet.", DateTimeOffset.UnixEpoch.ToString("O"));

    public ScannerStatusDto Current => Volatile.Read(ref _current);

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
        await hub.All.ScannerStatusChanged(dto).ConfigureAwait(false);
    }
}
