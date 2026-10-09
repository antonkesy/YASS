using Yass.App.Sessions;

namespace Yass.App.Export;

/// <summary>A produced document, redeemed from its one-shot token. The caller deletes or moves <see cref="Path"/>.</summary>
public sealed record ExportDownload(string Path, string ContentType, string DownloadName);

/// <summary>What <c>/export/{sessionId}/{file}</c> points to, whichever host serves it.</summary>
public sealed class ExportDownloads(DownloadTokens tokens)
{
    /// <summary>
    /// Redeems <paramref name="file"/> (<c>{token}{ext}</c>). The extension is the format's own, not
    /// always <c>.pdf</c>. <c>false</c> for an unknown format, a bad session id, a spent token or a missing file.
    /// </summary>
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
}
