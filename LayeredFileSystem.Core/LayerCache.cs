using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace LayeredFileSystem.Core;

public class LayerCache : ILayerCache
{
    private readonly IFileSystem _fileSystem;
    private readonly string _cacheDirectory;

    public LayerCache(IFileSystem fileSystem, string cacheDirectory)
    {
        _fileSystem = fileSystem;
        _cacheDirectory = cacheDirectory;
        
        if (!_fileSystem.Directory.Exists(_cacheDirectory))
        {
            _fileSystem.Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<bool> ExistsAsync(string hash)
    {
        var layerPath = GetLayerPath(hash);
        return _fileSystem.File.Exists(layerPath);
    }

    public async Task<Stream?> GetLayerAsync(string hash)
    {
        var layerPath = GetLayerPath(hash);
        
        if (!_fileSystem.File.Exists(layerPath))
        {
            return null;
        }

        return _fileSystem.File.OpenRead(layerPath);
    }

    public async Task StoreLayerAsync(string hash, Stream layerData)
    {
        var layerPath = GetLayerPath(hash);
        var tempPath = layerPath + ".tmp";
        
        try
        {
            // Write to temp file first
            using (var fileStream = _fileSystem.File.Create(tempPath))
            {
                await layerData.CopyToAsync(fileStream);
            }
            
            // Move temp file to final location
            if (_fileSystem.File.Exists(layerPath))
            {
                _fileSystem.File.Delete(layerPath);
            }
            _fileSystem.File.Move(tempPath, layerPath);
        }
        catch
        {
            // Clean up temp file on error
            if (_fileSystem.File.Exists(tempPath))
            {
                _fileSystem.File.Delete(tempPath);
            }
            throw;
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        var statistics = new CacheStatistics();
        
        if (!_fileSystem.Directory.Exists(_cacheDirectory))
        {
            return statistics;
        }

        var layerFiles = _fileSystem.Directory.GetFiles(_cacheDirectory, "*.tar");
        statistics.LayerCount = layerFiles.Length;
        
        foreach (var file in layerFiles)
        {
            var fileInfo = _fileSystem.FileInfo.New(file);
            statistics.TotalSizeBytes += fileInfo.Length;
            
            if (fileInfo.LastAccessTime > statistics.LastAccessed)
            {
                statistics.LastAccessed = fileInfo.LastAccessTime;
            }
        }

        return statistics;
    }

    private string GetLayerPath(string hash)
    {
        // Use first 2 characters of hash for subdirectory to avoid too many files in one directory
        var subdirectory = hash.Length >= 2 ? hash.Substring(0, 2) : "00";
        var subdirectoryPath = _fileSystem.Path.Combine(_cacheDirectory, subdirectory);
        
        if (!_fileSystem.Directory.Exists(subdirectoryPath))
        {
            _fileSystem.Directory.CreateDirectory(subdirectoryPath);
        }
        
        return _fileSystem.Path.Combine(subdirectoryPath, $"{hash}.tar");
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var stream = _fileSystem.File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}