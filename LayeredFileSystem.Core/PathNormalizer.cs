using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class PathNormalizer(IFileSystem fileSystem) : IPathNormalizer
{
    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Replace all directory separators with the current platform's separator
        var normalized = path.Replace('\\', fileSystem.Path.DirectorySeparatorChar)
                            .Replace('/', fileSystem.Path.DirectorySeparatorChar);

        // Remove any duplicate separators
        while (normalized.Contains($"{fileSystem.Path.DirectorySeparatorChar}{fileSystem.Path.DirectorySeparatorChar}"))
        {
            normalized = normalized.Replace(
                $"{fileSystem.Path.DirectorySeparatorChar}{fileSystem.Path.DirectorySeparatorChar}",
                fileSystem.Path.DirectorySeparatorChar.ToString());
        }

        // Remove leading separator for relative paths
        // For layered file system, we want all paths to be relative
        if (normalized.StartsWith(fileSystem.Path.DirectorySeparatorChar))
        {
            normalized = normalized.Substring(1);
        }

        // Remove trailing separator
        if (normalized.Length > 0 && normalized.EndsWith(fileSystem.Path.DirectorySeparatorChar))
        {
            normalized = normalized.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
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