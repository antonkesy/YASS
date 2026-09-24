namespace Yawss.Core.Export;

/// <summary>Places a finished document in the configured output directory, atomically and under a templated name.</summary>
public interface IDocumentStore
{
    /// <param name="write">Writes the document body into the stream it is given.</param>
    /// <param name="createdAt">Timestamp used for the filename template.</param>
    /// <param name="cancellationToken">Cancels the write; no partial file is left behind.</param>
    /// <returns>The final absolute path of the stored file.</returns>
    Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, DateTimeOffset createdAt, CancellationToken cancellationToken);
}
