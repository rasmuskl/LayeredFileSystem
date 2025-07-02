using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class LayerContext(
    IFileSystem fileSystem,
    string workingDirectory,
    string layerHash,
    bool isFromCache,
    ChangeDetector changeDetector,
    ILayerCache layerCache,
    ITarLayerWriter tarWriter,
    IPathNormalizer pathNormalizer,
    LayerSession session)
    : ILayerContext
{
    private bool _isFromCache = isFromCache;
    private DirectorySnapshot? _beforeSnapshot;
    private bool _disposed;
    private bool _committed;
    private bool _initialized;

    internal async Task InitializeAsync()
    {
        if (_initialized) return;
        
        // Take a snapshot of the current state BEFORE applying cached layer
        _beforeSnapshot = await changeDetector.CreateSnapshotAsync(workingDirectory);
        
        // Check if we have a cached layer and apply it if found
        _isFromCache = await CheckAndApplyCachedLayerAsync();
        
        _initialized = true;
    }

    public bool IsFromCache
    {
        get
        {
            if (!_initialized)
                throw new InvalidOperationException("LayerContext must be initialized before accessing IsFromCache. Call InitializeAsync first.");
            return _isFromCache;
        }
    }

    public async Task<LayerInfo> CommitAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerContext));

        if (_committed)
            throw new LayeredFileSystemException("Layer context has already been committed");

        await InitializeAsync();

        if (_isFromCache)
        {
            // For cached layers, we don't need to do anything more
            _committed = true;
            return new LayerInfo
            {
                Hash = layerHash,
                LayerPath = string.Empty,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = 0,
                Statistics = new LayerStatistics()
            };
        }

        // Take a snapshot of the current state and detect changes
        var afterSnapshot = await changeDetector.CreateSnapshotAsync(workingDirectory);
        var changes = await changeDetector.DetectChangesAsync(_beforeSnapshot!, afterSnapshot);

        if (changes.Count == 0)
        {
            // No changes, create an empty layer
            _committed = true;
            return new LayerInfo
            {
                Hash = layerHash,
                LayerPath = string.Empty,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = 0,
                Statistics = new LayerStatistics()
            };
        }

        // Create the layer and store it in cache
        using var layerStream = new MemoryStream();
        await tarWriter.CreateLayerAsync(changes, workingDirectory, layerStream);
        
        layerStream.Position = 0;
        await layerCache.StoreLayerAsync(layerHash, layerStream);

        var statistics = CalculateStatistics(changes);
        var layerInfo = new LayerInfo
        {
            Hash = layerHash,
            LayerPath = string.Empty, // We don't expose the cache path
            CreatedAt = DateTime.UtcNow,
            SizeBytes = layerStream.Length,
            Statistics = statistics
        };

        session.AddAppliedLayer(layerInfo);
        _committed = true;
        
        return layerInfo;
    }

    public async Task CancelAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerContext));

        if (_committed)
            throw new LayeredFileSystemException("Cannot cancel a committed layer context");

        await InitializeAsync();

        // Simply mark as disposed without creating or caching any layer
        // The working directory state is left as-is for the user to manage
        _disposed = true;
    }

    private LayerStatistics CalculateStatistics(IReadOnlyList<FileChange> changes)
    {
        var statistics = new LayerStatistics();
        
        foreach (var change in changes)
        {
            switch (change.Type)
            {
                case ChangeType.Added:
                    if (change.IsDirectory)
                        statistics.DirectoriesAdded++;
                    else
                        statistics.FilesAdded++;
                    break;
                    
                case ChangeType.Modified:
                    if (!change.IsDirectory)
                        statistics.FilesModified++;
                    break;
                    
                case ChangeType.Deleted:
                    if (change.IsDirectory)
                        statistics.DirectoriesDeleted++;
                    else
                        statistics.FilesDeleted++;
                    break;
            }
        }
        
        return statistics;
    }

    private bool IsDirectoryPath(string path)
    {
        // Simple heuristic - paths without extensions are likely directories
        return !fileSystem.Path.HasExtension(path);
    }


    public void Dispose()
    {
        if (!_disposed)
        {
            // No automatic cleanup - user is responsible for managing working directory state
            _disposed = true;
        }
    }

    private async Task<bool> CheckAndApplyCachedLayerAsync()
    {
        if (await layerCache.ExistsAsync(layerHash))
        {
            using var layerStream = await layerCache.GetLayerAsync(layerHash);
            if (layerStream != null)
            {
                // Apply the cached layer to the working directory
                var tarReader = new TarLayerReader(fileSystem, pathNormalizer);
                await tarReader.ApplyLayerAsync(layerStream, workingDirectory);
                
                // Create layer info for this cached layer
                var layerInfo = new LayerInfo
                {
                    Hash = layerHash,
                    LayerPath = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    SizeBytes = layerStream.Length,
                    Statistics = new LayerStatistics() // We don't track stats for cached layers
                };
                
                session.AddAppliedLayer(layerInfo);
                return true;
            }
        }
        
        return false;
    }
}