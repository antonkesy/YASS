using System.Globalization;
using Yass.Core.Export;

namespace Yass.Fake;

/// <summary>Writes PDFs to a directory without a filename template, for tests and <c>--fake</c> mode.</summary>
public sealed class FakeDocumentStore(string directory) : IDocumentStore
{
    public string Directory { get; } = directory;

    public async Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        System.IO.Directory.CreateDirectory(Directory);
        string final = Unique(Path.Combine(Directory, $"fake_{createdAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.pdf"));
        string temp = final + ".part";

        await using (FileStream stream = File.Create(temp))
        {
            await write(stream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, final, overwrite: true);
        return final;
    }

    /// <summary>
    /// Every document from one session shares a timestamp, so a session that splits into
    /// several would otherwise write them all to the same name and keep only the last.
    /// </summary>
    private static string Unique(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string candidate = path;
        for (int n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem}-{n}{extension}");
        }

        return candidate;
    }
}
