using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class LayeredFileSystem(IFileSystem? fileSystem = null) : ILayeredFileSystem
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

    /// <summary>
    /// Create a new layered file system session with the specified directories
    /// </summary>
    /// <param name="workingDirectory">Empty directory for layer operations</param>
    /// <param name="cacheDirectory">Directory for storing cached layers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new layer session</returns>
    public static async Task<ILayerSession> StartSession(string workingDirectory, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        var fileSystem = new LayeredFileSystem();
        return await fileSystem.CreateSessionAsync(workingDirectory, cacheDirectory, cancellationToken);
    }

    /// <summary>
    /// Create a new layered file system session using temporary directories
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new layer session with temporary working and cache directories</returns>
    public static async Task<ILayerSession> StartTemporarySession(CancellationToken cancellationToken = default)
    {
        var tempDir = Path.GetTempPath();
        var workingDir = Path.Combine(tempDir, "layered-fs-working", Guid.NewGuid().ToString());
        var cacheDir = Path.Combine(tempDir, "layered-fs-cache");
        
        var fileSystem = new LayeredFileSystem();
        return await fileSystem.CreateSessionAsync(workingDir, cacheDir, cancellationToken);
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

        var pathNormalizer = new PathNormalizer();
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