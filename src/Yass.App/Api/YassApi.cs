using Yass.App.Config;
using Yass.App.Export;
using Yass.App.Scanning;
using Yass.App.Sessions;
using Yass.Core.Config;
using Yass.Core.Domain;
using Yass.Core.Hub;
using Yass.Core.Scanning;

namespace Yass.App.Api;

/// <summary>The hub contract without a transport; the SignalR hub and the desktop bridge both delegate here.</summary>
public sealed class YassApi(
    ScannerStatusMonitor scanner,
    IScannerBackend backend,
    EffectiveOptions options,
    ConfigStore config,
    SessionService sessions,
    SessionMapper mapper,
    ScanService scans,
    EditService editor,
    AutoCropService cropper,
    MergeService merger,
    ExportService exporter,
    IClientNotifier clients,
    ILogger<YassApi> logger)
{
    public async Task<SessionDto> StartScan(ScanOptionsDto? requested)
    {
        SessionDto dto = mapper.ToDto(Guard(() => scans.Start(options.Resolve(requested))));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public Task StopScan(string sessionId)
    {
        scans.Stop(sessionId);
        return Task.CompletedTask;
    }

    public Task<SessionDto> GetSession(string sessionId) => Task.FromResult(mapper.ToDto(Find(sessionId)));

    public Task<SessionDto[]> ListSessions() => Task.FromResult(sessions.List());

    public Task DeleteSession(string sessionId) => GuardAsync(() => sessions.DeleteSession(sessionId));

    public Task<SessionDto> RenameSession(string sessionId, string? name) =>
        GuardAsync(() => sessions.RenameSession(sessionId, name));

    public Task<SessionDto> DiscardSession(string sessionId) =>
        GuardAsync(() => sessions.DiscardSession(sessionId));

    public Task<int> ClearSessions() => GuardAsync(sessions.ClearSessions);

    public async Task<SessionDto> ApplyEdits(string sessionId, PageEdit[] edits)
    {
        Find(sessionId);
        return mapper.ToDto(await GuardAsync(() => editor.ApplyAsync(sessionId, edits ?? [])).ConfigureAwait(false));
    }

    public async Task<int> AutoCrop(string sessionId, int[]? targets, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        return await GuardAsync(() => cropper.CropAsync(sessionId, targets, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<SessionDto> RescanPage(string sessionId, int pageIndex, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        await GuardAsync(() => scans.RescanPageAsync(sessionId, pageIndex, cancellationToken)).ConfigureAwait(false);
        return mapper.ToDto(Find(sessionId));
    }

    public async Task<SessionDto> ScanMore(string sessionId)
    {
        Find(sessionId);
        SessionDto dto = mapper.ToDto(Guard(() => scans.StartMore(sessionId)));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> MergeSession(string targetId, string sourceId)
    {
        Find(targetId);
        Find(sourceId);
        SessionDto dto = mapper.ToDto(Guard(() => merger.Merge(targetId, sourceId)));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<ExportResult> Export(string sessionId, ExportRequest request, CancellationToken cancellationToken = default)
    {
        Find(sessionId);
        return await GuardAsync(() => exporter.ExportAsync(sessionId, request ?? new ExportRequest(), cancellationToken), "Export failed: ").ConfigureAwait(false);
    }

    public Task<ScannerStatusDto> GetScannerStatus() => Task.FromResult(scanner.Current);

    public async Task<ScannerDeviceDto[]> ListDevices(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScannerDevice> devices = await backend.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        return [.. devices.Select(d => new ScannerDeviceDto(d.Id, d.Name, d.Driver, d.Connection))];
    }

    public Task<ScanOptionsDto> GetEffectiveOptions() => Task.FromResult(options.Current());

    public Task<string> GetServerFolder() =>
        Task.FromResult(config.Current.Output.SavesToServer() ? config.Current.Output.Directory : "");

    public Task ReloadConfig()
    {
        try
        {
            config.Reload();
            return Task.CompletedTask;
        }
        catch (ConfigValidationException ex)
        {
            logger.LogWarning("Config reload rejected: {Message}", ex.Message);
            throw new YassException(ex.Message, ex);
        }
    }

    private Session Find(string sessionId) => Guard(() => sessions.Get(sessionId));

    // BCL exceptions from the services become YassException for the caller; anything else is a bug and stays one.
    private static T Guard<T>(Func<T> work, string ioPrefix = "")
    {
        try
        {
            return work();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            throw new YassException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new YassException(ioPrefix + ex.Message, ex);
        }
    }

    private static async Task GuardAsync(Func<Task> work)
    {
        await GuardAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private static async Task<T> GuardAsync<T>(Func<Task<T>> work, string ioPrefix = "")
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            throw new YassException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new YassException(ioPrefix + ex.Message, ex);
        }
    }
}
