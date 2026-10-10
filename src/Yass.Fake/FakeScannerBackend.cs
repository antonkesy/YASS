using System.Runtime.CompilerServices;
using Yass.Core.Domain;
using Yass.Core.Scanning;

namespace Yass.Fake;

public sealed class FakeScannerBackend(FakeScenario scenario) : IScannerBackend
{
    private int _scanning;

    public FakeScenario Scenario { get; } = scenario;

    public ScanOptions? LastOptions { get; private set; }

    public Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ScannerDevice> devices = Scenario.NoDevice
            ? []
            : [.. Scenario.AllDevices.Select(name => new ScannerDevice(name, name, "fake"))];
        return Task.FromResult(devices);
    }

    public Task<ScannerStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        ScannerStatus status = Scenario.NoDevice
            ? new ScannerStatus(ScannerState.NoDevice, null, "No scanner found (fake).")
            : Volatile.Read(ref _scanning) == 1
                ? new ScannerStatus(ScannerState.Busy, Scenario.DeviceName, null)
                : new ScannerStatus(ScannerState.Ready, Scenario.DeviceName, null);
        return Task.FromResult(status);
    }

    public async IAsyncEnumerable<ScannedPage> ScanAsync(ScanOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (Scenario.NoDevice)
        {
            throw new InvalidOperationException("No scanner device available.");
        }

        if (Interlocked.Exchange(ref _scanning, 1) == 1)
        {
            throw new InvalidOperationException("Scanner is busy.");
        }

        LastOptions = options;
        if (options.Device.Length > 0 && !Scenario.AllDevices.Contains(options.Device, StringComparer.Ordinal))
        {
            Volatile.Write(ref _scanning, 0);
            throw new InvalidOperationException($"No scanner named '{options.Device}'.");
        }

        try
        {
            int count = options.Source == ScanSource.Flatbed ? Math.Min(1, Scenario.PageCount) : Scenario.PageCount;
            for (int number = 1; number <= count; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Scenario.ErrorAfter is { } after && number > after)
                {
                    throw new IOException($"Fake scanner jammed after page {after}.");
                }

                if (Scenario.PageDelay > TimeSpan.Zero)
                {
                    await Task.Delay(Scenario.PageDelay, cancellationToken).ConfigureAwait(false);
                }

                bool blank = Scenario.BlankPages.Contains(number);
                byte[] png = blank ? Fixtures.Load(Fixtures.BlankPage) : Fixtures.ContentPage(number);
                yield return new ScannedPage(number, Fixtures.AtDpi(png, options.Dpi), blank && options.BlankPageDetection);
            }
        }
        finally
        {
            Volatile.Write(ref _scanning, 0);
        }
    }
}
