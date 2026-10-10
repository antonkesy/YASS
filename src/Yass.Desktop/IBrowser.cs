namespace Yass.Desktop;

/// <summary>The webview opens no windows of its own, so links leaving the app go here.</summary>
public interface IBrowser
{
    void Open(Uri url);
}
