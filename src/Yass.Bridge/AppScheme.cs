using System.Web;
using Yass.App.Export;
using Yass.App.Rendering;

namespace Yass.Bridge;

/// <summary>The stream is the webview's to close.</summary>
public sealed record SchemeResponse(Stream Body, string ContentType);

/// <summary>
/// <c>yass://app</c>, except on Windows and Android: Photino does not register a custom scheme with WebView2, which
/// then shows a blank window, and Android's WebView only intercepts http(s). There it is <c>https://yass.localhost</c>,
/// answered by the same handler; nothing listens on a socket.
/// </summary>
public sealed record AppOrigin(string Scheme, string Host)
{
    public static readonly AppOrigin Custom = new("yass", "app");

    public static readonly AppOrigin Https = new("https", "yass.localhost");

    public static AppOrigin ForThisPlatform() => OperatingSystem.IsWindows() || OperatingSystem.IsAndroid() ? Https : Custom;

    public Uri Root => new($"{Scheme}://{Host}/");

    public bool Owns(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsAbsoluteUri
            && string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>The server's HTTP paths; anything else that is not a file gets <c>index.html</c> (SPA route).</summary>
public sealed class AppScheme(IStaticFiles files, ImageSource images, ExportDownloads downloads, AppOrigin origin)
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
        if (segments.Any(s => s is "." or ".." || s.Contains('/', StringComparison.Ordinal) || s.Contains('\\', StringComparison.Ordinal)))
        {
            return null;
        }

        string path = string.Join('/', segments);
        Stream? body = path.Length == 0 ? null : files.Open(path);
        if (body is null)
        {
            if (Path.HasExtension(path))
            {
                return null;
            }

            path = "index.html";
            body = files.Open(path);
            if (body is null)
            {
                return null;
            }
        }

        string type = ContentTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
        return new SchemeResponse(body, type);
    }
}
