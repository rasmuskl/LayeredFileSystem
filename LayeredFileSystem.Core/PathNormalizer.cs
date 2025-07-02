using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class PathNormalizer : IPathNormalizer
{
    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Always use forward slashes for normalized paths (platform-independent)
        var normalized = path.Replace('\\', '/');

        // Remove any duplicate separators
        while (normalized.Contains("//"))
        {
            normalized = normalized.Replace("//", "/");
        }

        // Remove leading separator for relative paths
        // For layered file system, we want all paths to be relative
        if (normalized.StartsWith("/"))
        {
            normalized = normalized.Substring(1);
        }

        // Remove trailing separator
        if (normalized.Length > 0 && normalized.EndsWith("/"))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    public bool HasDuplicate(string path, IEnumerable<string> existingPaths)
    {
        var normalizedPath = NormalizePath(path);
        return existingPaths.Any(existing => 
            string.Equals(NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }
}