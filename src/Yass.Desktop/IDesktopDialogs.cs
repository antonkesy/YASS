namespace Yass.Desktop;

/// <summary><c>null</c> means the user cancelled.</summary>
public interface IDesktopDialogs
{
    /// <summary>An empty <paramref name="name"/> leaves the dialog's name field empty.</summary>
    Task<string?> SaveFileAsync(string folder, string name, string extension);

    Task<string?> PickFolderAsync(string startFolder);
}
