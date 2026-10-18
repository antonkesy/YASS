using Yass.Core.Domain;

namespace Yass.Core.Scanning;

/// <summary>Implementations must not hold more than one decoded page in memory at a time.</summary>
public interface IScannerBackend
{
    Task<ScannerStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enumeration is slow on SANE and eSCL, so implementations cache it; <paramref name="refresh"/>
    /// looks again now, for a scanner plugged in since.
    /// </summary>
    Task<IReadOnlyList<ScannerDevice>> ListDevicesAsync(bool refresh, CancellationToken cancellationToken);

    IAsyncEnumerable<ScannedPage> ScanAsync(ScanOptions options, CancellationToken cancellationToken);
}
