using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class LayeredFileSystem : ILayeredFileSystem
{
    private readonly IFileSystem _fileSystem;

    public LayeredFileSystem(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<ILayerSession> CreateSessionAsync(string workingDirectory, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("Working directory cannot be null or empty", nameof(workingDirectory));
        
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            throw new ArgumentException("Cache directory cannot be null or empty", nameof(cacheDirectory));

        // Ensure working directory exists and is empty
        if (_fileSystem.Directory.Exists(workingDirectory))
        {
            var files = _fileSystem.Directory.GetFileSystemEntries(workingDirectory);
            if (files.Length > 0)
            {
                throw new LayeredFileSystemException($"Working directory '{workingDirectory}' must be empty");
            }
        }
        else
        {
            _fileSystem.Directory.CreateDirectory(workingDirectory);
        }

        // Ensure cache directory exists
        if (!_fileSystem.Directory.Exists(cacheDirectory))
        {
            _fileSystem.Directory.CreateDirectory(cacheDirectory);
        }

        var pathNormalizer = new PathNormalizer(_fileSystem);
        var changeDetector = new ChangeDetector(_fileSystem, pathNormalizer);
        var layerCache = new LayerCache(_fileSystem, cacheDirectory);
        var tarReader = new TarLayerReader(_fileSystem, pathNormalizer);
        var tarWriter = new TarLayerWriter(_fileSystem, pathNormalizer);

        return new LayerSession(
            _fileSystem,
            workingDirectory,
            changeDetector,
            layerCache,
            tarReader,
            tarWriter,
            pathNormalizer);
    }
}