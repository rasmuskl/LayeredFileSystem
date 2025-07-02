using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class LayerContext : ILayerContext
{
    private readonly IFileSystem _fileSystem;
    private readonly string _workingDirectory;
    private readonly string _layerHash;
    private bool _isFromCache;
    private readonly ChangeDetector _changeDetector;
    private readonly ILayerCache _layerCache;
    private readonly ITarLayerWriter _tarWriter;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly LayerSession _session;
    private DirectorySnapshot? _beforeSnapshot;
    private bool _disposed;
    private bool _committed;
    private bool _initialized;

    public LayerContext(
        IFileSystem fileSystem,
        string workingDirectory,
        string layerHash,
        bool isFromCache,
        ChangeDetector changeDetector,
        ILayerCache layerCache,
        ITarLayerWriter tarWriter,
        IPathNormalizer pathNormalizer,
        LayerSession session)
    {
        _fileSystem = fileSystem;
        _workingDirectory = workingDirectory;
        _layerHash = layerHash;
        _changeDetector = changeDetector;
        _layerCache = layerCache;
        _tarWriter = tarWriter;
        _pathNormalizer = pathNormalizer;
        _session = session;
    }

    internal async Task InitializeAsync()
    {
        if (_initialized) return;
        
        // Take a snapshot of the current state BEFORE applying cached layer
        _beforeSnapshot = await _changeDetector.CreateSnapshotAsync(_workingDirectory);
        
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
                Hash = _layerHash,
                LayerPath = string.Empty,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = 0,
                Statistics = new LayerStatistics()
            };
        }

        // Take a snapshot of the current state and detect changes
        var afterSnapshot = await _changeDetector.CreateSnapshotAsync(_workingDirectory);
        var changes = await _changeDetector.DetectChangesAsync(_beforeSnapshot!, afterSnapshot);

        if (changes.Count == 0)
        {
            // No changes, create an empty layer
            _committed = true;
            return new LayerInfo
            {
                Hash = _layerHash,
                LayerPath = string.Empty,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = 0,
                Statistics = new LayerStatistics()
            };
        }

        // Create the layer and store it in cache
        using var layerStream = new MemoryStream();
        await _tarWriter.CreateLayerAsync(changes, _workingDirectory, layerStream);
        
        layerStream.Position = 0;
        await _layerCache.StoreLayerAsync(_layerHash, layerStream);

        var statistics = CalculateStatistics(changes);
        var layerInfo = new LayerInfo
        {
            Hash = _layerHash,
            LayerPath = string.Empty, // We don't expose the cache path
            CreatedAt = DateTime.UtcNow,
            SizeBytes = layerStream.Length,
            Statistics = statistics
        };

        _session.AddAppliedLayer(layerInfo);
        _committed = true;
        
        return layerInfo;
    }

    public async Task RollbackAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerContext));

        if (_committed)
            throw new LayeredFileSystemException("Cannot rollback a committed layer context");

        await InitializeAsync();

        // Restore the working directory to the before state
        // This works for both cached and non-cached layers since we took the snapshot before applying cached layers
        await RestoreDirectoryStateAsync(_beforeSnapshot!);
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
        return !_fileSystem.Path.HasExtension(path);
    }

    private async Task RestoreDirectoryStateAsync(DirectorySnapshot snapshot)
    {
        // For rollback, we need to recreate the exact state from the snapshot
        // Since we don't store file contents in snapshots, we'll use a different approach:
        // Detect what changes were made and reverse them
        
        var currentSnapshot = await _changeDetector.CreateSnapshotAsync(_workingDirectory);
        var changes = await _changeDetector.DetectChangesAsync(snapshot, currentSnapshot);
        
        // Reverse the changes to get back to the original state
        foreach (var change in changes.Reverse())
        {
            var fullPath = _fileSystem.Path.Combine(_workingDirectory, change.RelativePath);
            
            switch (change.Type)
            {
                case ChangeType.Added:
                    // If something was added, remove it
                    if (_fileSystem.File.Exists(fullPath))
                    {
                        _fileSystem.File.Delete(fullPath);
                    }
                    else if (_fileSystem.Directory.Exists(fullPath))
                    {
                        _fileSystem.Directory.Delete(fullPath, recursive: true);
                    }
                    break;
                    
                case ChangeType.Deleted:
                    // If something was deleted, we can't restore it without content
                    // This is a limitation of the current snapshot approach
                    // In a real implementation, you'd need to store file contents or use a different rollback strategy
                    break;
                    
                case ChangeType.Modified:
                    // If something was modified, we can't restore original content
                    // This is also a limitation
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_initialized && !_committed && !_isFromCache)
            {
                // Auto-rollback if not committed
                try
                {
                    RollbackAsync().Wait();
                }
                catch
                {
                    // Ignore rollback errors during dispose
                }
            }
            
            _disposed = true;
        }
    }

    private async Task<bool> CheckAndApplyCachedLayerAsync()
    {
        if (await _layerCache.ExistsAsync(_layerHash))
        {
            using var layerStream = await _layerCache.GetLayerAsync(_layerHash);
            if (layerStream != null)
            {
                // Apply the cached layer to the working directory
                var tarReader = new TarLayerReader(_fileSystem, _pathNormalizer);
                await tarReader.ApplyLayerAsync(layerStream, _workingDirectory);
                
                // Create layer info for this cached layer
                var layerInfo = new LayerInfo
                {
                    Hash = _layerHash,
                    LayerPath = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    SizeBytes = layerStream.Length,
                    Statistics = new LayerStatistics() // We don't track stats for cached layers
                };
                
                _session.AddAppliedLayer(layerInfo);
                return true;
            }
        }
        
        return false;
    }
}