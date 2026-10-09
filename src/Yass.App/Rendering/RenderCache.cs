using Yass.App.Sessions;
using Yass.Core.Domain;

namespace Yass.App.Rendering;

public enum ImageKind
{
    Thumb = 0,
    Preview,

    /// <summary>
    /// The page at preview size with every edit but its crop and skew: what the Crop dialog draws
    /// its box over. A crop is taken from the scan as it came, so drawing it over a page already
    /// cropped — or straightened, which crops too — put the box in the wrong place.
    /// </summary>
    Frame,
}

/// <summary>
/// Disk cache of derived images under <c>{session}/cache/{hash}-{kind}.webp</c>. Because the file name is the
/// content hash, a cached file is valid forever and the HTTP layer can mark it immutable.
/// </summary>
public sealed class RenderCache(SessionStore store, ImageRenderer renderer, Config.ConfigStore config) : IThumbnailStatus
{
    public string PathFor(string sessionId, string hash, ImageKind kind) =>
        Path.Combine(SessionPaths.Cache(store.Root, sessionId), $"{hash}-{kind.ToString().ToLowerInvariant()}.webp");

    public bool IsReady(Session session, Page page)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);
        return File.Exists(PathFor(session.Id, page.Hash, ImageKind.Thumb));
    }

    /// <summary>Returns the cached file path, rendering it first if needed. Concurrent renders of the same file are harmless (temp + rename).</summary>
    public async Task<string> GetOrRenderAsync(Session session, Page page, ImageKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);

        string path = PathFor(session.Id, page.Hash, kind);
        if (File.Exists(path))
        {
            return path;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int width = kind == ImageKind.Thumb ? config.Current.Processing.ThumbnailWidth : config.Current.Processing.PreviewMaxWidth;
        Page rendered = kind == ImageKind.Frame ? page with { Crop = null, Skew = 0 } : page;
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream output = File.Create(temp))
            {
                await renderer.RenderAsync(SessionPaths.Source(store.Root, session, page), rendered, session.OptionsUsed.Color, width, output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return path;
    }
}
