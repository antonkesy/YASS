namespace Yass.Core.Export;

/// <param name="CreatedAt">Timestamp the filename template expands against.</param>
/// <param name="FileName">Base name without extension, or <c>null</c> to use the template.</param>
/// <param name="Directory">Absolute target directory, or <c>null</c> for the configured one.</param>
/// <param name="Extension">File extension including the dot, e.g. <c>.pdf</c>.</param>
public sealed record DocumentTarget(DateTimeOffset CreatedAt, string? FileName, string? Directory, string Extension);

/// <summary>Places a finished document in the configured output directory, atomically and under a templated name.</summary>
public interface IDocumentStore
{
    /// <param name="write">Writes the document body into the stream it is given.</param>
    /// <param name="target">Where it goes and what it is called.</param>
    /// <param name="cancellationToken">Cancels the write; no partial file is left behind.</param>
    /// <returns>The final absolute path of the stored file.</returns>
    Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, DocumentTarget target, CancellationToken cancellationToken);
}
