namespace LayeredFileSystem.Core;

public interface ILayeredFileSystem
{
    /// <summary>
    /// Initialize a new layered file system session
    /// </summary>
    /// <param name="workingDirectory">Empty directory for operations</param>
    /// <param name="cacheDirectory">Directory for storing cached layers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ILayerSession> CreateSessionAsync(string workingDirectory, string cacheDirectory, CancellationToken cancellationToken = default);
}

public interface ILayerSession : IDisposable
{
    /// <summary>
    /// Begin a new layer step
    /// </summary>
    /// <param name="inputHash">Hash for cache lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Layer context for this step</returns>
    Task<ILayerContext> BeginLayerAsync(string inputHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current working directory
    /// </summary>
    string WorkingDirectory { get; }
    
    /// <summary>
    /// Get all applied layers in order
    /// </summary>
    IReadOnlyList<LayerInfo> AppliedLayers { get; }
}

public interface ILayerContext : IDisposable
{
    /// <summary>
    /// True if this layer was loaded from cache
    /// </summary>
    bool IsFromCache { get; }
    
    /// <summary>
    /// Complete the layer and create a snapshot
    /// </summary>
    Task<LayerInfo> CommitAsync();
    
    /// <summary>
    /// Cancel this layer without creating or caching a snapshot.
    /// The working directory state is left as-is for the user to manage.
    /// </summary>
    Task CancelAsync();
}

public interface IChangeDetector
{
    /// <summary>
    /// Detect changes between two directory states
    /// </summary>
    Task<IReadOnlyList<FileChange>> DetectChangesAsync(
        DirectorySnapshot before, 
        DirectorySnapshot after
    );
}

public interface ITarLayerWriter
{
    /// <summary>
    /// Create a TAR layer from file changes
    /// </summary>
    Task CreateLayerAsync(
        IReadOnlyList<FileChange> changes,
        string baseDirectory,
        Stream outputStream
    );
}

public interface ITarLayerReader
{
    /// <summary>
    /// Apply a TAR layer to a directory
    /// </summary>
    Task ApplyLayerAsync(
        Stream layerStream,
        string targetDirectory
    );
}

public interface ILayerCache
{
    /// <summary>
    /// Check if a layer exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string hash);
    
    /// <summary>
    /// Get a cached layer stream
    /// </summary>
    Task<Stream?> GetLayerAsync(string hash);
    
    /// <summary>
    /// Store a new layer in cache
    /// </summary>
    Task StoreLayerAsync(string hash, Stream layerData);
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();
}

public interface IPathNormalizer
{
    /// <summary>
    /// Normalize path for cross-platform compatibility
    /// </summary>
    string NormalizePath(string path);
    
    /// <summary>
    /// Check for case-insensitive duplicates
    /// </summary>
    bool HasDuplicate(string path, IEnumerable<string> existingPaths);
}