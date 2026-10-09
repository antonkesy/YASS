using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NAPS2.Images;
using NAPS2.Images.ImageSharp;
using NAPS2.Scan;
using NAPS2.Scan.Exceptions;
using Yass.Core.Config;
using Yass.Core.Scanning;

namespace Yass.Scanning;

/// <summary>
/// <see cref="IScannerBackend"/> over NAPS2.Sdk with the headless ImageSharp image context (no GTK).
/// Device discovery is cached for the poll interval because SANE/eSCL enumeration is slow.
/// </summary>
public sealed class Naps2ScannerBackend : IScannerBackend, IDisposable
{
    private readonly ScanningContext _context;
    private readonly ScanController _controller;
    private readonly Func<ScannerConfig> _config;
    private readonly ILogger<Naps2ScannerBackend> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _discovery = new(1, 1);
    private (DateTimeOffset At, ScanDevice? Device, IReadOnlyList<ScanDevice> Devices, string? Error) _cache = (DateTimeOffset.MinValue, null, [], null);
    private int _scanning;

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
            return new ScannerStatus(ScannerState.Busy, _cache.Device?.Name, null);
        }

        (ScanDevice? device, string? error) = await FindDeviceAsync(force: false, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return new ScannerStatus(ScannerState.Error, null, error);
        }

        return device is null
            ? new ScannerStatus(ScannerState.NoDevice, null, NoDeviceMessage())
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
            (ScanDevice? device, string? error) = await FindDeviceAsync(force: true, cancellationToken).ConfigureAwait(false);
            if (options.Device.Length > 0)
            {
                // The browser asked for one by id. Not falling back to the configured device on a
                // miss is deliberate: scanning the wrong machine is worse than not scanning.
                device = _cache.Devices.FirstOrDefault(d => string.Equals(d.ID, options.Device, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"No scanner with id '{options.Device}'.");
            }

            if (device is null)
            {
                throw new InvalidOperationException(error ?? NoDeviceMessage());
            }

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

                    using ProcessedImage image = pages.Current;
                    byte[] png = ToPng(image);
                    yield return new ScannedPage(++number, png, IsBlank: false);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _scanning, 0);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _discovery.Dispose();
    }

    /// <summary>Renders the scanned page once and encodes it as PNG; the ProcessedImage is disposed by the caller.</summary>
    private static byte[] ToPng(ProcessedImage image)
    {
        using IMemoryImage rendered = image.Render();
        using MemoryStream stream = rendered.SaveToMemoryStream(ImageFileFormat.Png);
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        await FindDeviceAsync(force: false, cancellationToken).ConfigureAwait(false);
        return [.. _cache.Devices.Select(d => new ScannerDevice(d.ID, d.Name, d.Driver.ToString().ToLowerInvariant(), string.IsNullOrWhiteSpace(d.ConnectionUri) ? null : d.ConnectionUri))];
    }

    private async Task<(ScanDevice? Device, string? Error)> FindDeviceAsync(bool force, CancellationToken cancellationToken)
    {
        ScannerConfig config = _config();
        // Enumerating SANE backends can take tens of seconds; never re-probe more often than every 30 s
        // on the status poll. Starting a scan always forces a fresh look.
        TimeSpan ttl = TimeSpan.FromSeconds(Math.Max(config.PollIntervalSeconds, 30));
        if (!force && _time.GetUtcNow() - _cache.At < ttl)
        {
            return (_cache.Device, _cache.Error);
        }

        await _discovery.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && _time.GetUtcNow() - _cache.At < ttl)
            {
                return (_cache.Device, _cache.Error);
            }

            try
            {
                List<ScanDevice> found = await _controller
                    .GetDeviceList(new NAPS2.Scan.ScanOptions { Driver = OptionMapper.MapDriver(config.Driver) })
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                IReadOnlyList<ScanDevice> devices = OptionMapper.ScannersOnly(found);
                if (devices.Count < found.Count)
                {
                    _logger.LogDebug("Ignoring cameras: {Cameras}", string.Join(", ", found.Where(OptionMapper.IsCamera).Select(d => $"{d.Name} [{d.ID}]")));
                }

                ScanDevice? device = OptionMapper.SelectDevice(devices, config.Device);
                if (device is null && devices.Count > 0)
                {
                    _logger.LogWarning("No device matches '{Configured}'; found: {Devices}", config.Device, string.Join(", ", devices.Select(d => $"{d.Name} [{d.ID}]")));
                }

                _cache = (_time.GetUtcNow(), device, devices, null);
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
                _cache = (_time.GetUtcNow(), null, [], ex.Message);
            }

            return (_cache.Device, _cache.Error);
        }
        finally
        {
            _discovery.Release();
        }
    }

    private string NoDeviceMessage()
    {
        ScannerConfig config = _config();
        return string.IsNullOrWhiteSpace(config.Device)
            ? $"No scanner found (driver: {config.Driver})."
            : $"No scanner matching '{config.Device}' (driver: {config.Driver}).";
    }
}
