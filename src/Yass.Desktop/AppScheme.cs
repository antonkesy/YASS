using System.Web;
using Yass.App.Export;
using Yass.App.Rendering;

namespace Yass.Desktop;

/// <summary>The stream is the webview's to close.</summary>
public sealed record SchemeResponse(Stream Body, string ContentType);

/// <summary>
/// <c>yass://app</c>, except on Windows: Photino does not register a custom scheme with WebView2, which then shows
/// a blank window. There it is <c>https://yass.localhost</c>, answered by the same handler; nothing listens on a socket.
/// </summary>
public sealed record AppOrigin(string Scheme, string Host)
{
    public static readonly AppOrigin Custom = new("yass", "app");

    public static readonly AppOrigin Windows = new("https", "yass.localhost");

    public static AppOrigin ForThisPlatform() => OperatingSystem.IsWindows() ? Windows : Custom;

    public Uri Root => new($"{Scheme}://{Host}/");

    public bool Owns(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsAbsoluteUri
            && string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>The server's HTTP paths, read off disk; anything else that is not a file gets <c>index.html</c> (SPA route).</summary>
public sealed class AppScheme(string webRoot, ImageSource images, ExportDownloads downloads, AppOrigin origin)
{
    public AppOrigin Origin { get; } = origin;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json",
        [".webmanifest"] = "application/manifest+json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".webp"] = "image/webp",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    private readonly string _root = Path.GetFullPath(webRoot);

    /// <summary>Null when nothing is there, or when the URL is not the app's own (on Windows another https host), which the webview then fetches itself.</summary>
    public SchemeResponse? Resolve(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!Origin.Owns(uri))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        return segments switch
        {
            ["img", string session, string page, string kind] => Image(session, page, kind, HttpUtility.ParseQueryString(uri.Query)["h"], cancellationToken),
            ["export", string session, string file] => Export(session, file),
            _ => Static(segments),
        };
    }

    private SchemeResponse? Image(string sessionId, string page, string kind, string? hash, CancellationToken cancellationToken)
    {
        if (!int.TryParse(page, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index)
            || !images.TryResolve(sessionId, index, kind, hash, out ImageRequest request))
        {
            return null;
        }

        // The webview asks synchronously; thumbnails are rendered ahead, so this only waits on an unseen preview or frame.
        string? path = images.GetFileAsync(request, cancellationToken).GetAwaiter().GetResult();
        return path is null ? null : new SchemeResponse(File.OpenRead(path), "image/webp");
    }

    private SchemeResponse? Export(string sessionId, string file) =>
        downloads.TryRedeem(sessionId, file, out ExportDownload download)
            // One-shot, as over HTTP: the file goes once the webview has read it.
            ? new SchemeResponse(new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Delete, 81920, FileOptions.DeleteOnClose), download.ContentType)
            : null;

    private SchemeResponse? Static(string[] segments)
    {
        string candidate = Path.GetFullPath(Path.Combine([_root, .. segments]));
        bool inside = candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || candidate == _root;
        if (!inside)
        {
            return null;
        }

        if (!File.Exists(candidate))
        {
            if (Path.HasExtension(candidate) && candidate != _root)
            {
                return null;
            }

            candidate = Path.Combine(_root, "index.html");
            if (!File.Exists(candidate))
            {
                return null;
            }
        }

        string type = ContentTypes.GetValueOrDefault(Path.GetExtension(candidate), "application/octet-stream");
        return new SchemeResponse(File.OpenRead(candidate), type);
    }
}
