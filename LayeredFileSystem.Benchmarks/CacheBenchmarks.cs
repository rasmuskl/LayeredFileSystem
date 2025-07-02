using BenchmarkDotNet.Attributes;
using LayeredFileSystem.Core;
using System.IO.Abstractions;

namespace LayeredFileSystem.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class CacheBenchmarks
{
    private IFileSystem _fileSystem = null!;
    private string _workingDir = null!;
    private string _cacheDir = null!;
    private string _preparedLayerHash = null!;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _fileSystem = new FileSystem();
        _workingDir = Path.Combine(Path.GetTempPath(), "benchmark_working_" + Guid.NewGuid());
        _cacheDir = Path.Combine(Path.GetTempPath(), "benchmark_cache_" + Guid.NewGuid());
        
        _fileSystem.Directory.CreateDirectory(_workingDir);
        _fileSystem.Directory.CreateDirectory(_cacheDir);
        
        // Pre-populate cache with a layer for cache hit tests
        await PrepareLayer();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_fileSystem.Directory.Exists(_workingDir))
            _fileSystem.Directory.Delete(_workingDir, true);
        if (_fileSystem.Directory.Exists(_cacheDir))
            _fileSystem.Directory.Delete(_cacheDir, true);
    }

    private async Task PrepareLayer()
    {
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        
        // Create a layer with moderate complexity
        using var context = await session.BeginLayerAsync("benchmark-layer-prep");
        
        if (!context.IsFromCache)
        {
            // Add some files and directories
            await _fileSystem.File.WriteAllTextAsync(
                Path.Combine(_workingDir, "file1.txt"), 
                "Content for benchmarking");
            await _fileSystem.File.WriteAllTextAsync(
                Path.Combine(_workingDir, "file2.txt"), 
                new string('x', 1000)); // 1KB file
            
            _fileSystem.Directory.CreateDirectory(Path.Combine(_workingDir, "subdir"));
            await _fileSystem.File.WriteAllTextAsync(
                Path.Combine(_workingDir, "subdir", "nested.txt"), 
                "Nested content");
        }
        
        var layerInfo = await context.CommitAsync();
        _preparedLayerHash = "benchmark-layer-prep";
        
        // Clean working directory for tests
        foreach (var item in _fileSystem.Directory.EnumerateFileSystemEntries(_workingDir))
        {
            if (_fileSystem.File.Exists(item))
                _fileSystem.File.Delete(item);
            else
                _fileSystem.Directory.Delete(item, true);
        }
    }

    [Benchmark]
    public async Task<string> CacheMiss_CreateLayer()
    {
        var uniqueHash = $"unique-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create unique content to ensure cache miss
            var uniqueContent = $"Content_{Guid.NewGuid()}";
            await _fileSystem.File.WriteAllTextAsync(
                Path.Combine(_workingDir, "unique.txt"), 
                uniqueContent);
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up for next iteration
        if (_fileSystem.File.Exists(Path.Combine(_workingDir, "unique.txt")))
            _fileSystem.File.Delete(Path.Combine(_workingDir, "unique.txt"));
        
        return uniqueHash;
    }

    [Benchmark]
    public async Task CacheHit_ApplyExistingLayer()
    {
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(_preparedLayerHash);
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up for next iteration
        foreach (var item in _fileSystem.Directory.EnumerateFileSystemEntries(_workingDir))
        {
            if (_fileSystem.File.Exists(item))
                _fileSystem.File.Delete(item);
            else
                _fileSystem.Directory.Delete(item, true);
        }
    }

    [Benchmark]
    public async Task<string> CacheMiss_CreateAndApplyLayer()
    {
        var uniqueHash = $"unique-{Guid.NewGuid()}";
        
        // Create layer (cache miss)
        using (var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var context = await session.BeginLayerAsync(uniqueHash);
            if (!context.IsFromCache)
            {
                var uniqueContent = $"Content_{Guid.NewGuid()}";
                await _fileSystem.File.WriteAllTextAsync(
                    Path.Combine(_workingDir, "unique.txt"), 
                    uniqueContent);
            }
            await context.CommitAsync();
        }
        
        // Clean working directory
        if (_fileSystem.File.Exists(Path.Combine(_workingDir, "unique.txt")))
            _fileSystem.File.Delete(Path.Combine(_workingDir, "unique.txt"));
        
        // Apply the same layer (should be cache hit now)
        using (var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var context = await session.BeginLayerAsync(uniqueHash);
            await context.CommitAsync();
        }
        
        // Clean up for next iteration
        if (_fileSystem.File.Exists(Path.Combine(_workingDir, "unique.txt")))
            _fileSystem.File.Delete(Path.Combine(_workingDir, "unique.txt"));
        
        return uniqueHash;
    }

    [Benchmark]
    public async Task LargeFile_CacheMiss()
    {
        var uniqueHash = $"large-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create a 1MB file
            var largeContent = new string('A', 1024 * 1024);
            var fileName = $"large_{Guid.NewGuid()}.txt";
            await _fileSystem.File.WriteAllTextAsync(
                Path.Combine(_workingDir, fileName), 
                largeContent);
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        foreach (var file in _fileSystem.Directory.GetFiles(_workingDir))
        {
            _fileSystem.File.Delete(file);
        }
    }

    [Benchmark]
    public async Task MultipleFiles_CacheMiss()
    {
        var uniqueHash = $"multi-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create 50 small files
            for (int i = 0; i < 50; i++)
            {
                var fileName = $"file_{i}.txt";
                await _fileSystem.File.WriteAllTextAsync(
                    Path.Combine(_workingDir, fileName), 
                    $"Content for file {i}");
            }
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        foreach (var file in _fileSystem.Directory.GetFiles(_workingDir))
        {
            _fileSystem.File.Delete(file);
        }
    }
}