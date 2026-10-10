using Yass.App.Sessions;

namespace Yass.App.Export;

/// <summary>The caller deletes or moves <see cref="Path"/>.</summary>
public sealed record ExportDownload(string Path, string ContentType, string DownloadName);

public sealed class ExportDownloads(DownloadTokens tokens)
{
    public bool TryRedeem(string sessionId, string file, out ExportDownload download)
    {
        download = null!;
        string extension = System.IO.Path.GetExtension(file);
        string? contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            _ => null,
        };

        if (contentType is null
            || !SessionPaths.IsValidId(sessionId)
            || !tokens.TryRedeem(sessionId, System.IO.Path.GetFileNameWithoutExtension(file), out string path)
            || !File.Exists(path))
        {
            return false;
        }

        download = new ExportDownload(path, contentType, $"yass-{sessionId}{extension}");
        return true;
    }

    // For a download whose file could not be used (a failed move), so the same URL can be retried.
    public void Return(string sessionId, string file, ExportDownload download)
    {
        ArgumentNullException.ThrowIfNull(download);
        tokens.Restore(sessionId, System.IO.Path.GetFileNameWithoutExtension(file), download.Path);
    }
}
