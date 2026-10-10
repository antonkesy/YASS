namespace Yass.App.Export;

public interface IDownloadPaths
{
    /// <summary>The extension keeps its dot.</summary>
    string Download(string sessionId, string token, string extension);
}

public sealed class DownloadPaths(string prefix = "") : IDownloadPaths
{
    private readonly string _prefix = prefix.TrimEnd('/');

    public string Download(string sessionId, string token, string extension) => $"{_prefix}/export/{sessionId}/{token}{extension}";
}
