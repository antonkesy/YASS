using Yass.App.Api;
using Yass.App.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.App.Sessions;

/// <summary>Deletes sessions older than <c>sessions.retention_hours</c>, checking hourly. 0 disables.</summary>
public sealed class SessionRetentionService(
    SessionStore store,
    ConfigStore config,
    IClientNotifier hub,
    TimeProvider time,
    ILogger<SessionRetentionService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync().ConfigureAwait(false);
            try
            {
                await Task.Delay(Interval, time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task SweepAsync()
    {
        int hours = config.Current.Sessions.RetentionHours;
        if (hours <= 0)
        {
            return;
        }

        DateTimeOffset cutoff = time.GetUtcNow().AddHours(-hours);
        foreach (Session session in store.List())
        {
            if (session.CreatedAt >= cutoff || session.State is SessionState.Scanning or SessionState.Exporting)
            {
                continue;
            }

            if (store.Delete(session.Id))
            {
                logger.LogInformation("Deleted expired session {Session}", session.Id);
                await hub.All.SessionChanged(new SessionDto(session.Id, session.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture), SessionState.Done, [], Scanning.EffectiveOptions.ToDto(session.OptionsUsed), "expired", session.Name, [], 0)).ConfigureAwait(false);
            }
        }
    }
}
