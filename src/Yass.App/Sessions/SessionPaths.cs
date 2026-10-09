using System.Globalization;
using Yass.Core.Domain;

namespace Yass.App.Sessions;

/// <summary>Layout of a session directory: <c>session.json</c>, <c>page-NNN.png</c>, <c>cache/</c>, <c>export/</c>.</summary>
public static class SessionPaths
{
    public const string ManifestName = "session.json";

    public static string Directory(string root, string sessionId) => Path.Combine(root, sessionId);

    public static string Manifest(string root, string sessionId) => Path.Combine(Directory(root, sessionId), ManifestName);

    public static string PageFileName(int number) => $"page-{number.ToString("D3", CultureInfo.InvariantCulture)}.png";

    public static string Source(string root, Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);
        return Path.Combine(Directory(root, session.Id), page.SourceFile);
    }

    public static string Cache(string root, string sessionId) => Path.Combine(Directory(root, sessionId), "cache");

    public static string Export(string root, string sessionId) => Path.Combine(Directory(root, sessionId), "export");

    /// <summary>
    /// Bytes the session occupies: source pages, derived caches and any pending export.
    /// Read from disk rather than tracked, so it cannot drift from what is actually there.
    /// </summary>
    public static long SizeOnDisk(string root, string sessionId)
    {
        var dir = new DirectoryInfo(Directory(root, sessionId));
        if (!dir.Exists)
        {
            return 0;
        }

        try
        {
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch (IOException)
        {
            // A scan writing pages underneath us is not a reason to fail a listing.
            return 0;
        }
    }

    /// <summary>Sortable, URL-safe id: timestamp plus a short random suffix.</summary>
    public static string NewId(DateTimeOffset now) =>
        now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6];

    public static bool IsValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
