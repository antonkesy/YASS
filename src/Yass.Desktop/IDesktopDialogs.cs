namespace Yass.Desktop;

/// <summary><c>null</c> means the user cancelled.</summary>
public interface IDesktopDialogs
{
    Task<string?> SaveFileAsync(string suggestedPath, string extension);

    Task<string?> PickFolderAsync(string startFolder);
}
