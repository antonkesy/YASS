using Microsoft.Extensions.Logging;
using Yass.Core.Config;
using Yass.Core.Export;

namespace Yass.Export;

/// <summary>
/// Writes finished PDFs into the consume directory atomically: the body goes to a hidden <c>._yass-*.tmp</c>
/// file in the same directory (matched by paperless-ngx's default ignore pattern), then a single rename
/// publishes it under the templated name. A consumer never sees a partial PDF. Existing names get a numeric suffix.
/// </summary>
public sealed class ConsumeDirectoryStore(Func<OutputConfig> config, ILogger<ConsumeDirectoryStore> logger) : IDocumentStore
{
    public const string TempPrefix = "._yass-";

    public async Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, DocumentTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(target);
        OutputConfig output = config();
        string directory = string.IsNullOrWhiteSpace(target.Directory) ? output.Directory : target.Directory;
        Directory.CreateDirectory(directory);

        string temp = Path.Combine(directory, TempPrefix + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA1849 // fsync has no async counterpart; the rename must not outrun the data.
                stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
            }

            string fileName = string.IsNullOrWhiteSpace(target.FileName)
                ? FilenameTemplate.Expand(output.FilenameTemplate, target.CreatedAt, extension: target.Extension)
                : FilenameTemplate.Expand(target.FileName, target.CreatedAt, extension: target.Extension);
            string final = UniquePath(directory, fileName);
            File.Move(temp, final);
            logger.LogInformation("Stored {File}", final);
            return final;
        }
        catch
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            throw;
        }
    }

    /// <summary><c>scan.pdf</c>, then <c>scan-2.pdf</c>, <c>scan-3.pdf</c>, … until free.</summary>
    internal static string UniquePath(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        for (int n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem}-{n}{ext}");
        }

        return candidate;
    }
}
