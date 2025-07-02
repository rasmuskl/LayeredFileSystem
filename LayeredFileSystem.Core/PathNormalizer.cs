using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class PathNormalizer : IPathNormalizer
{
    private readonly IFileSystem _fileSystem;

    public PathNormalizer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Replace all directory separators with the current platform's separator
        var normalized = path.Replace('\\', _fileSystem.Path.DirectorySeparatorChar)
                            .Replace('/', _fileSystem.Path.DirectorySeparatorChar);

        // Remove any duplicate separators
        while (normalized.Contains($"{_fileSystem.Path.DirectorySeparatorChar}{_fileSystem.Path.DirectorySeparatorChar}"))
        {
            normalized = normalized.Replace(
                $"{_fileSystem.Path.DirectorySeparatorChar}{_fileSystem.Path.DirectorySeparatorChar}",
                _fileSystem.Path.DirectorySeparatorChar.ToString());
        }

        // Remove leading separator for relative paths
        // For layered file system, we want all paths to be relative
        if (normalized.StartsWith(_fileSystem.Path.DirectorySeparatorChar))
        {
            normalized = normalized.Substring(1);
        }

        // Remove trailing separator
        if (normalized.Length > 0 && normalized.EndsWith(_fileSystem.Path.DirectorySeparatorChar))
        {
            normalized = normalized.TrimEnd(_fileSystem.Path.DirectorySeparatorChar);
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