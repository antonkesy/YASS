using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Yass.App.Export;

/// <summary>One-shot download URLs: a token maps to a file exactly once.</summary>
public sealed class DownloadTokens
{
    private readonly ConcurrentDictionary<string, (string SessionId, string Path)> _tokens = new(StringComparer.Ordinal);

    public string Issue(string sessionId, string path)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _tokens[token] = (sessionId, path);
        return token;
    }

    public bool TryPeekPath(string token, out string path)
    {
        bool found = _tokens.TryGetValue(token, out (string SessionId, string Path) entry);
        path = found ? entry.Path : "";
        return found;
    }

    /// <summary>Redeems the token; a second call returns <c>false</c>.</summary>
    public bool TryRedeem(string sessionId, string token, out string path)
    {
        path = "";
        if (_tokens.TryRemove(token, out (string SessionId, string Path) entry) && entry.SessionId == sessionId)
        {
            path = entry.Path;
            return true;
        }

        return false;
    }
}
