using Yawss.Core.Domain;

namespace Yawss.Core.Scanning;

/// <summary>
/// A scanner driver. Implemented by the NAPS2 adapter and by the fake used in tests and <c>--fake</c> mode.
/// Implementations must not hold more than one decoded page in memory at a time.
/// </summary>
public interface IScannerBackend
{
    /// <summary>Cheap readiness probe; polled on <c>scanner.poll_interval_seconds</c>.</summary>
    Task<ScannerStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Scans until the feeder is empty (or the flatbed page is done) or the token is cancelled.
    /// Each item is one page as PNG bytes; the enumerable ends when there is nothing left to scan.
    /// </summary>
    IAsyncEnumerable<ScannedPage> ScanAsync(ScanOptions options, CancellationToken cancellationToken);
}
