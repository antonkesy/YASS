using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NAPS2.Images;
using NAPS2.Images.ImageSharp;
using NAPS2.Scan;
using NAPS2.Scan.Exceptions;
using Yass.Core.Config;
using Yass.Core.Scanning;

namespace Yass.Scanning;

public sealed class Naps2ScannerBackend : IScannerBackend, IDisposable
{
    private readonly ScanningContext _context;
    private readonly ScanController _controller;
    private readonly Func<ScannerConfig> _config;
    private readonly ILogger<Naps2ScannerBackend> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _discovery = new(1, 1);
    // One immutable snapshot, swapped whole, so readers outside the lock never mix two looks.
    private DeviceCache _cache = new(DateTimeOffset.MinValue, null, [], null);
    private int _scanning;
    private string? _scanningOn;

    public Naps2ScannerBackend(Func<ScannerConfig> config, ILogger<Naps2ScannerBackend> logger, TimeProvider? time = null)
    {
        _config = config;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _context = new ScanningContext(new ImageSharpImageContext()) { Logger = logger };
        _controller = new ScanController(_context) { PropagateErrors = true };
    }

    public async Task<ScannerStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _scanning) == 1)
        {
            return new ScannerStatus(ScannerState.Busy, Volatile.Read(ref _scanningOn), null);
        }

        (_, ScanDevice? device, _, string? error) = await FindDeviceAsync(force: false, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return new ScannerStatus(ScannerState.Error, null, error);
        }

        return device is null
            ? new ScannerStatus(ScannerState.NoDevice, null, NoDeviceDetail())
            : new ScannerStatus(ScannerState.Ready, device.Name, null);
    }

    public async IAsyncEnumerable<ScannedPage> ScanAsync(Core.Domain.ScanOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (Interlocked.Exchange(ref _scanning, 1) == 1)
        {
            throw new InvalidOperationException("Scanner is busy.");
        }

        try
        {
            (_, ScanDevice? device, IReadOnlyList<ScanDevice> devices, string? error) = await FindDeviceAsync(force: true, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                throw new InvalidOperationException(error);
            }

            if (options.Device.Length > 0)
            {
                // No fallback to the configured device on a miss: scanning the wrong machine is worse than not scanning.
                device = devices.FirstOrDefault(d => string.Equals(d.ID, options.Device, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"No scanner with id '{options.Device}'.");
            }

            if (device is null)
            {
                throw new InvalidOperationException(NoDeviceMessage());
            }

            Volatile.Write(ref _scanningOn, device.Name);

            NAPS2.Scan.ScanOptions naps2 = OptionMapper.ToNaps2(options, _config().Driver, device);
            _logger.LogInformation("Scanning on {Device} ({Driver}) at {Dpi} dpi, {Source}, {Depth}", device.Name, naps2.Driver, naps2.Dpi, naps2.PaperSource, naps2.BitDepth);

            int number = 0;
            IAsyncEnumerator<ProcessedImage> pages = _controller.Scan(naps2, cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (pages.ConfigureAwait(false))
            {
                while (true)
                {
                    bool more;
                    try
                    {
                        more = await pages.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (DeviceFeederEmptyException) when (number > 0)
                    {
                        break;
                    }
                    catch (ScanDriverException ex)
                    {
                        throw new IOException(ex.Message, ex);
                    }

                    if (!more)
                    {
                        break;
                    }

                    // Released before the page is handed on: the decoded scan is tens of MB.
                    byte[] png;
                    using (ProcessedImage image = pages.Current)
                    {
                        png = ToPng(image);
                    }

                    yield return new ScannedPage(++number, png, IsBlank: false);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _scanningOn, null);
            Volatile.Write(ref _scanning, 0);
        }
    }

    private static readonly IReadOnlySet<string> EmptyIds = new HashSet<string>();

    // If WIA cannot say, nothing is filtered: an empty list from a failed lookup would stop every scan.
    private IReadOnlySet<string> WiaCameras()
    {
        if (!OperatingSystem.IsWindows())
        {
            return EmptyIds;
        }

        try
        {
            return WiaDeviceKinds.CameraIds(TimeSpan.FromSeconds(10));
        }
#pragma warning disable CA1031 // See above: a failed lookup must not cost the scanners.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Could not ask WIA which devices are cameras; listing them all");
            return EmptyIds;
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _discovery.Dispose();
    }

    private static byte[] ToPng(ProcessedImage image)
    {
        using IMemoryImage rendered = image.Render();
        using MemoryStream stream = rendered.SaveToMemoryStream(ImageFileFormat.Png);
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        DeviceCache found = await FindDeviceAsync(force: false, cancellationToken).ConfigureAwait(false);
        return [.. found.Devices.Select(d => new ScannerDevice(d.ID, d.Name, d.Driver.ToString().ToLowerInvariant(), string.IsNullOrWhiteSpace(d.ConnectionUri) ? null : d.ConnectionUri))];
    }

    private async Task<DeviceCache> FindDeviceAsync(bool force, CancellationToken cancellationToken)
    {
        ScannerConfig config = _config();
        // SANE enumeration can take tens of seconds; never re-probe more often than every 30 s.
        TimeSpan ttl = TimeSpan.FromSeconds(Math.Max(config.PollIntervalSeconds, 30));
        DeviceCache cached = _cache;
        if (!force && _time.GetUtcNow() - cached.At < ttl)
        {
            return cached;
        }

        await _discovery.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cache;
            if (!force && _time.GetUtcNow() - cached.At < ttl)
            {
                return cached;
            }

            try
            {
                List<ScanDevice> found = await _controller
                    .GetDeviceList(new NAPS2.Scan.ScanOptions { Driver = OptionMapper.MapDriver(config.Driver) })
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                IReadOnlySet<string> wiaCameras = found.Any(d => d.Driver == Driver.Wia) ? WiaCameras() : EmptyIds;
                IReadOnlyList<ScanDevice> devices = OptionMapper.ScannersOnly(found, wiaCameras);
                if (devices.Count < found.Count)
                {
                    _logger.LogDebug("Ignoring cameras: {Cameras}", string.Join(", ", found.Where(d => OptionMapper.IsCamera(d, wiaCameras)).Select(d => $"{d.Name} [{d.ID}]")));
                }

                ScanDevice? device = OptionMapper.SelectDevice(devices, config.Device);
                if (device is null && devices.Count > 0)
                {
                    _logger.LogWarning("No device matches '{Configured}'; found: {Devices}", config.Device, string.Join(", ", devices.Select(d => $"{d.Name} [{d.ID}]")));
                }

                _cache = new DeviceCache(_time.GetUtcNow(), device, devices, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Driver enumeration failures are reported as status, not crashes.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex, "Scanner discovery failed");
                _cache = new DeviceCache(_time.GetUtcNow(), null, [], ex.Message);
            }

            return _cache;
        }
        finally
        {
            _discovery.Release();
        }
    }

    private sealed record DeviceCache(DateTimeOffset At, ScanDevice? Device, IReadOnlyList<ScanDevice> Devices, string? Error);

    private string NoDeviceMessage() => "No scanner found. " + NoDeviceDetail();

    private string NoDeviceDetail()
    {
        ScannerConfig config = _config();
        string with = OptionMapper.DriverLabel(config.Driver);
        return string.IsNullOrWhiteSpace(config.Device)
            ? $"Looked with {with}."
            : $"Nothing matches \u201c{config.Device}\u201d. Looked with {with}.";
    }
}
