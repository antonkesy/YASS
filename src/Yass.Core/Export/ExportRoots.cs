namespace Yass.Core.Export;

/// <summary>
/// Decides whether a directory the browser asked for is one this server will write to.
///
/// YASS has no authentication, so the export folder is the one place a LAN client names an
/// arbitrary write path. Containment is checked after resolving symlinks on both sides: a
/// link sitting inside an allowed root and pointing outside it would otherwise be a way
/// straight through the check.
/// </summary>
public static class ExportRoots
{
    /// <summary>
    /// The roots actually in force: the configured ones, or <paramref name="outputDirectory"/>
    /// alone when none are configured.
    /// </summary>
    public static IReadOnlyList<string> Effective(IReadOnlyList<string>? allowedRoots, string outputDirectory)
    {
        string[] configured = [.. (allowedRoots ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim())];
        return configured.Length > 0 ? configured : [outputDirectory];
    }

    /// <summary>True when <paramref name="candidate"/> is one of the roots or lies beneath one.</summary>
    public static bool Allows(IReadOnlyList<string> roots, string candidate)
    {
        ArgumentNullException.ThrowIfNull(roots);
        string target = Resolve(candidate);
        return roots.Any(root => IsWithin(Resolve(root), target));
    }

    /// <summary>
    /// Absolute, normalised, and followed through symlinks. Resolution walks the path one
    /// segment at a time because a link can sit anywhere along it, not only at the end:
    /// <c>/data/link/deeper</c> has to follow <c>link</c> even though <c>deeper</c> is not
    /// itself a link and may not exist yet. A path that does not exist is still normalised,
    /// so <c>..</c> cannot be used to climb out.
    /// </summary>
    internal static string Resolve(string path)
    {
        string full = Path.GetFullPath(path);
        string prefix = Path.GetPathRoot(full) ?? "";
        string current = prefix;

        foreach (string segment in full[prefix.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                FileSystemInfo? target = Directory.ResolveLinkTarget(current, returnFinalTarget: true)
                    ?? File.ResolveLinkTarget(current, returnFinalTarget: true);
                if (target is not null)
                {
                    current = Path.GetFullPath(target.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not there yet, broken, circular, or unreadable: keep the literal segment.
                // Anything that matters is caught again by the existence and write checks.
            }
        }

        return current.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static bool IsWithin(string root, string candidate)
    {
        if (string.Equals(root, candidate, PathComparison))
        {
            return true;
        }

        // The separator matters: "/data/consume-other" must not count as inside "/data/consume".
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
