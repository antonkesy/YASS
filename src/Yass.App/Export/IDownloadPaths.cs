namespace Yass.App.Export;

/// <summary>How the host spells the one-shot URL of a produced document.</summary>
public interface IDownloadPaths
{
    /// <summary>The URL that redeems <paramref name="token"/>; the extension (<c>.pdf</c>, <c>.jpg</c>, <c>.png</c>) keeps its dot.</summary>
    string Download(string sessionId, string token, string extension);
}

/// <summary><c>/export/{sessionId}/{token}{ext}</c> under an optional path prefix.</summary>
public sealed class DownloadPaths(string prefix = "") : IDownloadPaths
{
    private readonly string _prefix = prefix.TrimEnd('/');

    public string Download(string sessionId, string token, string extension) => $"{_prefix}/export/{sessionId}/{token}{extension}";
}
