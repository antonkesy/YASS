namespace Yass.Bridge;

/// <summary>The web build: a folder on desktop, the APK's assets on Android.</summary>
public interface IStaticFiles
{
    /// <summary><paramref name="path"/> is relative, '/'-separated and has no <c>..</c>; null when there is no such file.</summary>
    Stream? Open(string path);
}

public sealed class DiskStaticFiles(string root) : IStaticFiles
{
    private readonly string _root = Path.GetFullPath(root);

    public Stream? Open(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string candidate = Path.GetFullPath(Path.Combine(_root, path));
        bool inside = candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return inside && File.Exists(candidate) ? File.OpenRead(candidate) : null;
    }
}
