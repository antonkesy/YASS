namespace Yass.Desktop;

/// <summary>The system's web browser, which links leaving the app open in: the webview opens no windows of its own.</summary>
public interface IBrowser
{
    void Open(Uri url);
}
