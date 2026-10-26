using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.Provider;
using Android.Webkit;
using Yass.App.Export;
using Yass.Bridge;
using Yass.Core.Export;
using AndroidUri = Android.Net.Uri;

namespace Yass.Android;

/// <summary>
/// The desktop host's methods (<c>DesktopApi</c>), so the page saves the same way on both: through Android's own
/// file and folder pickers, with the host copying the document the export wrote. A document never passes through
/// the page.
/// </summary>
/// <remarks>
/// A picked file comes back as <c>picked:n/name.pdf</c> rather than its <c>content://</c> URI, so the page can number it
/// for a second document (<c>picked:n/name-2.pdf</c>) like a path. The picker grants that one file only, so numbered
/// ones go to Downloads. A picked folder is its tree URI; the page appends <c>/name.pdf</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Needs a device: the system pickers and content providers.")]
public sealed class AndroidApi(MainActivity activity, ExportDownloads downloads, ILogger<AndroidApi> logger)
{
    private const string Picked = "picked:";

    private readonly Dictionary<string, AndroidUri> _picked = new(StringComparer.Ordinal);
    private int _picks;

    /// <summary>An empty <paramref name="suggestedName"/> opens the picker with no name filled in.</summary>
    public async Task<string?> PickSavePath(string? suggestedName, string? extension)
    {
        extension = string.IsNullOrWhiteSpace(extension) ? "" : "." + extension.Trim().TrimStart('.');
        string name = suggestedName?.Trim() ?? "";
        if (extension.Length > 0 && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length];
        }

        name = name.Length == 0 ? "" : FileNames.Safe(name);
        using var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(MimeType(extension));
        if (name.Length > 0)
        {
            intent.PutExtra(Intent.ExtraTitle, name + extension);
        }

        using Intent? result = await activity.StartForResultAsync(intent).ConfigureAwait(false);
        if (result?.Data is not { } uri)
        {
            return null;
        }

        string token = $"{Picked}{Interlocked.Increment(ref _picks)}/{DisplayName(uri) ?? name + extension}";
        lock (_picked)
        {
            _picked[token] = uri;
        }

        return token;
    }

    public async Task<string?> PickFolder()
    {
        using var intent = new Intent(Intent.ActionOpenDocumentTree);
        using Intent? result = await activity.StartForResultAsync(intent).ConfigureAwait(false);
        return result?.Data?.ToString();
    }

    /// <summary>Returns the name the document was saved under. A taken name is never replaced: the provider numbers it.</summary>
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
            string saved = Save(download, target);
            File.Delete(download.Path);
            logger.LogInformation("Saved {File}", saved);
            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Java.Lang.Exception or BridgeException)
        {
            // The document is still where the export left it: let the user pick somewhere else.
            downloads.Return(sessionId, file, download);
            throw new BridgeException($"Could not save {Path.GetFileName(target)}: {ex.Message}");
        }
    }

    /// <summary>Web addresses only, so the page cannot start another app or open a file.</summary>
    public void OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new BridgeException($"{url} is not a web address.");
        }

        activity.OpenExternal(url);
    }

    private string Save(ExportDownload download, string target)
    {
        AndroidUri? picked;
        lock (_picked)
        {
            _picked.TryGetValue(target, out picked);
        }

        if (picked is not null)
        {
            Copy(download.Path, picked);
            return target[(target.IndexOf('/', StringComparison.Ordinal) + 1)..];
        }

        int slash = target.LastIndexOf('/');
        string name = FileNames.Safe(target[(slash + 1)..]);
        if (target.StartsWith(Picked, StringComparison.Ordinal))
        {
            return SaveToDownloads(download, name);
        }

        if (slash > 0 && target.StartsWith("content://", StringComparison.Ordinal))
        {
            using AndroidUri tree = AndroidUri.Parse(target[..slash])!;
            using AndroidUri folder = DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!;
            using AndroidUri created = DocumentsContract.CreateDocument(Resolver, folder, download.ContentType, name)
                ?? throw new IOException("the folder did not take a new file");
            Copy(download.Path, created);
            return DisplayName(created) ?? name;
        }

        throw new BridgeException($"{target} is not a place this app can save to.");
    }

    private string SaveToDownloads(ExportDownload download, string name)
    {
        using var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, name);
        values.Put(MediaStore.IMediaColumns.MimeType, download.ContentType);
        using AndroidUri created = Resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new IOException("Downloads did not take a new file");
        Copy(download.Path, created);
        return "Download/" + (DisplayName(created) ?? name);
    }

    private void Copy(string from, AndroidUri to)
    {
        using Stream output = Resolver.OpenOutputStream(to, "wt") ?? throw new IOException($"could not write {to}");
        using FileStream input = File.OpenRead(from);
        input.CopyTo(output);
    }

    private string? DisplayName(AndroidUri uri)
    {
        try
        {
            using global::Android.Database.ICursor? cursor = Resolver.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            return cursor is not null && cursor.MoveToFirst() ? cursor.GetString(0) : null;
        }
        catch (Java.Lang.Exception ex)
        {
            logger.LogDebug(ex, "No display name for {Uri}", uri);
            return null;
        }
    }

    private ContentResolver Resolver => activity.ContentResolver!;

    private static string MimeType(string extension) =>
        MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension.TrimStart('.').ToLowerInvariant()) ?? "application/octet-stream";
}
