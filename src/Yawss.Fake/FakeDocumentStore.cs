using System.Globalization;
using Yawss.Core.Export;

namespace Yawss.Fake;

/// <summary>Drops documents into a directory as <c>fake_yyyyMMdd_HHmmss.pdf</c>. Stand-in until Yawss.Export provides templating.</summary>
public sealed class FakeDocumentStore(string directory) : IDocumentStore
{
    public string Directory { get; } = directory;

    public async Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        System.IO.Directory.CreateDirectory(Directory);
        string final = Path.Combine(Directory, $"fake_{createdAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.pdf");
        string temp = final + ".part";

        await using (FileStream stream = File.Create(temp))
        {
            await write(stream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, final, overwrite: true);
        return final;
    }
}
