using Yass.App.Export;
using Yass.Core.Export;

namespace Yass.Desktop;

/// <summary>
/// What only the desktop offers, callable over the bridge next to the hub methods: native save
/// dialogs, moving a finished export to where the user chose, and opening links in the browser.
/// A document never passes through the page: the host renames the file the export wrote.
/// </summary>
public sealed class DesktopApi(IDesktopDialogs dialogs, IBrowser browser, ExportDownloads downloads, ILogger<DesktopApi> logger)
{
    private string _lastFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);

    /// <summary>Asks where one document goes. <c>null</c> when the user cancels.</summary>
    public async Task<string?> PickSavePath(string suggestedName)
    {
        string name = FileNames.Safe(suggestedName ?? "scan");
        string extension = Path.GetExtension(name);
        string? chosen = await dialogs.SaveFileAsync(Path.Combine(_lastFolder, name), extension).ConfigureAwait(false);
        if (string.IsNullOrEmpty(chosen))
        {
            return null;
        }

        // A name typed without its extension still gets the format's own.
        if (!string.IsNullOrEmpty(extension) && !chosen.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            chosen += extension;
        }

        _lastFolder = Path.GetDirectoryName(chosen) ?? _lastFolder;
        return chosen;
    }

    /// <summary>Asks for a folder to save several documents into. <c>null</c> when the user cancels.</summary>
    public async Task<string?> PickFolder()
    {
        string? chosen = await dialogs.PickFolderAsync(_lastFolder).ConfigureAwait(false);
        if (string.IsNullOrEmpty(chosen))
        {
            return null;
        }

        _lastFolder = chosen;
        return chosen;
    }

    /// <summary>
    /// Moves the document behind a one-shot <paramref name="downloadPath"/> (as an export returns it)
    /// to <paramref name="target"/>. With <paramref name="overwrite"/> — the user picked that exact file
    /// and the dialog already asked — an existing file is replaced; without, the name gets -2, -3…
    /// Returns the path written.
    /// </summary>
    public string SaveExport(string downloadPath, string target, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrEmpty(downloadPath);
        ArgumentException.ThrowIfNullOrEmpty(target);

        string[] parts = downloadPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not ["export", string sessionId, string file] || !downloads.TryRedeem(sessionId, file, out ExportDownload download))
        {
            throw new BridgeException("That document has already been saved or is gone; export it again.");
        }

        try
        {
            string full = Path.GetFullPath(target);
            string directory = Path.GetDirectoryName(full) ?? throw new BridgeException($"{target} is not a file path.");
            Directory.CreateDirectory(directory);
            string destination = overwrite ? full : FileNames.Unique(directory, Path.GetFileName(full));
            File.Move(download.Path, destination, overwrite);
            logger.LogInformation("Saved {File}", destination);
            return destination;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BridgeException)
        {
            // The document is still where the export left it: let the user pick somewhere else.
            downloads.Return(sessionId, file, download);
            throw new BridgeException($"Could not save to {target}: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens <paramref name="url"/> in the system's browser: a link out of the app, such as the
    /// issue forms, which a webview with no windows of its own would otherwise swallow. Web
    /// addresses only, so the page cannot have the host start a program or open a file.
    /// </summary>
    public void OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new BridgeException($"{url} is not a web address.");
        }

        browser.Open(url);
    }
}
