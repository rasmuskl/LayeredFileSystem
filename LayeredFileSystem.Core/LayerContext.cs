using System.IO.Abstractions;
using System.Security.Cryptography;

namespace LayeredFileSystem.Core;

public class LayerContext : ILayerContext
{
    private readonly IFileSystem _fileSystem;
    private readonly string _workingDirectory;
    private readonly string _layerHash;
    private readonly bool _isFromCache;
    private readonly ChangeDetector _changeDetector;
    private readonly ILayerCache _layerCache;
    private readonly ITarLayerWriter _tarWriter;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly LayerSession _session;
    private readonly DirectorySnapshot _beforeSnapshot;
    private bool _disposed;
    private bool _committed;

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
        
        // Check if we have a cached layer and apply it if found
        _isFromCache = CheckAndApplyCachedLayerAsync().Result;
        
        // Take a snapshot of the current state
        _beforeSnapshot = _changeDetector.CreateSnapshotAsync(_workingDirectory).Result;
    }

    public bool IsFromCache => _isFromCache;

    public async Task<LayerInfo> CommitAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerContext));

        if (_committed)
            throw new LayeredFileSystemException("Layer context has already been committed");

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
        var changes = await _changeDetector.DetectChangesAsync(_beforeSnapshot, afterSnapshot);

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

        if (_isFromCache)
        {
            // For cached layers, we need to restore the previous state
            // This is complex and would require keeping track of what was applied
            // For now, we'll throw an exception
            throw new LayeredFileSystemException("Cannot rollback a cached layer that has already been applied");
        }

        // Restore the working directory to the before state
        await RestoreDirectoryStateAsync(_beforeSnapshot);
    }

    private LayerStatistics CalculateStatistics(IReadOnlyList<FileChange> changes)
    {
        var statistics = new LayerStatistics();
        
        foreach (var change in changes)
        {
            switch (change.Type)
            {
                case ChangeType.Added:
                    if (change.FileInfo?.Attributes.HasFlag(FileAttributes.Directory) == true)
                        statistics.DirectoriesAdded++;
                    else
                        statistics.FilesAdded++;
                    break;
                    
                case ChangeType.Modified:
                    statistics.FilesModified++;
                    break;
                    
                case ChangeType.Deleted:
                    if (IsDirectoryPath(change.RelativePath))
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
        // Clear the working directory
        if (_fileSystem.Directory.Exists(_workingDirectory))
        {
            foreach (var entry in _fileSystem.Directory.GetFileSystemEntries(_workingDirectory))
            {
                if (_fileSystem.File.Exists(entry))
                {
                    _fileSystem.File.Delete(entry);
                }
                else if (_fileSystem.Directory.Exists(entry))
                {
                    _fileSystem.Directory.Delete(entry, recursive: true);
                }
            }
        }

        // Restore files and directories from snapshot
        foreach (var (path, metadata) in snapshot.Files)
        {
            var fullPath = _fileSystem.Path.Combine(_workingDirectory, path);
            
            if (metadata.IsDirectory)
            {
                if (!_fileSystem.Directory.Exists(fullPath))
                {
                    _fileSystem.Directory.CreateDirectory(fullPath);
                }
            }
            else
            {
                // This is a simplified restoration - in a real implementation,
                // you'd need to store the actual file contents in the snapshot
                var directoryPath = _fileSystem.Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directoryPath) && !_fileSystem.Directory.Exists(directoryPath))
                {
                    _fileSystem.Directory.CreateDirectory(directoryPath);
                }
                
                // Create empty file with correct metadata
                _fileSystem.File.WriteAllText(fullPath, string.Empty);
                _fileSystem.File.SetLastWriteTime(fullPath, metadata.LastWriteTime);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (!_committed && !_isFromCache)
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

    private async Task<string> CalculateTarHashAsync(Stream tarStream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(tarStream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
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