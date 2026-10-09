namespace Yass.Desktop;

/// <summary>The native file dialogs, as the desktop's save flow needs them. <c>null</c> means the user cancelled.</summary>
public interface IDesktopDialogs
{
    /// <param name="suggestedPath">Folder and file name the dialog opens with.</param>
    /// <param name="extension">The format's extension with its dot, to filter by.</param>
    Task<string?> SaveFileAsync(string suggestedPath, string extension);

    Task<string?> PickFolderAsync(string startFolder);
}
