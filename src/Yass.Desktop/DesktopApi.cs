using Yass.App.Export;
using Yass.Core.Export;

namespace Yass.Desktop;

/// <summary>A document never passes through the page: the host renames the file the export wrote.</summary>
public sealed class DesktopApi(IDesktopDialogs dialogs, IBrowser browser, ExportDownloads downloads, ILogger<DesktopApi> logger)
{
    private string _lastFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);

    /// <summary>An empty <paramref name="suggestedName"/> opens the dialog with no name filled in.</summary>
    public async Task<string?> PickSavePath(string? suggestedName, string? extension)
    {
        extension = string.IsNullOrWhiteSpace(extension) ? "" : "." + extension.Trim().TrimStart('.');
        string name = suggestedName?.Trim() ?? "";
        if (extension.Length > 0 && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length];
        }

        name = name.Length == 0 ? "" : FileNames.Safe(name);
        string? chosen = await dialogs.SaveFileAsync(_lastFolder, name, extension).ConfigureAwait(false);
        if (string.IsNullOrEmpty(chosen))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(extension) && !chosen.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            chosen += extension;
        }

        _lastFolder = Path.GetDirectoryName(chosen) ?? _lastFolder;
        return chosen;
    }

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

    /// <summary>With <paramref name="overwrite"/> (the dialog already asked) an existing file is replaced; otherwise the name gets -2, -3…</summary>
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

    /// <summary>A webview with no windows of its own would swallow outbound links. Web addresses only, so the page cannot start a program or open a file.</summary>
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
