using BenchmarkDotNet.Attributes;
using LayeredFileSystem.Core;
using System.IO.Abstractions;

namespace LayeredFileSystem.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class OverheadBenchmarks
{
    private IFileSystem _fileSystem = null!;
    private string _workingDir = null!;
    private string _cacheDir = null!;
    private string _baselineDir = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fileSystem = new FileSystem();
        _workingDir = Path.Combine(Path.GetTempPath(), "overhead_working_" + Guid.NewGuid());
        _cacheDir = Path.Combine(Path.GetTempPath(), "overhead_cache_" + Guid.NewGuid());
        _baselineDir = Path.Combine(Path.GetTempPath(), "overhead_baseline_" + Guid.NewGuid());
        
        _fileSystem.Directory.CreateDirectory(_workingDir);
        _fileSystem.Directory.CreateDirectory(_cacheDir);
        _fileSystem.Directory.CreateDirectory(_baselineDir);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_fileSystem.Directory.Exists(_workingDir))
            _fileSystem.Directory.Delete(_workingDir, true);
        if (_fileSystem.Directory.Exists(_cacheDir))
            _fileSystem.Directory.Delete(_cacheDir, true);
        if (_fileSystem.Directory.Exists(_baselineDir))
            _fileSystem.Directory.Delete(_baselineDir, true);
    }

    [Benchmark(Baseline = true)]
    public async Task Baseline_DirectFileOperations()
    {
        // Direct file system operations without layered system
        var content = "Test content for baseline measurement";
        var fileName = Path.Combine(_baselineDir, "test.txt");
        
        await _fileSystem.File.WriteAllTextAsync(fileName, content);
        var readContent = await _fileSystem.File.ReadAllTextAsync(fileName);
        _fileSystem.File.Delete(fileName);
        
        // Create a directory and nested file
        var subDir = Path.Combine(_baselineDir, "subdir");
        _fileSystem.Directory.CreateDirectory(subDir);
        var nestedFile = Path.Combine(subDir, "nested.txt");
        await _fileSystem.File.WriteAllTextAsync(nestedFile, content);
        _fileSystem.Directory.Delete(subDir, true);
    }

    [Benchmark]
    public async Task LayeredSystem_SingleFileOperation()
    {
        var uniqueHash = $"single-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            var content = "Test content for layered system measurement";
            var fileName = Path.Combine(_workingDir, "test.txt");
            await _fileSystem.File.WriteAllTextAsync(fileName, content);
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        var fileName2 = Path.Combine(_workingDir, "test.txt");
        if (_fileSystem.File.Exists(fileName2))
            _fileSystem.File.Delete(fileName2);
    }

    [Benchmark]
    public async Task LayeredSystem_CreateAndApplyLayer()
    {
        var uniqueHash = $"create-apply-{Guid.NewGuid()}";
        
        // Create layer
        using (var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var createContext = await session.BeginLayerAsync(uniqueHash);
            if (!createContext.IsFromCache)
            {
                var content = $"Content_{Guid.NewGuid()}";
                var fileName = Path.Combine(_workingDir, "test.txt");
                await _fileSystem.File.WriteAllTextAsync(fileName, content);
            }
            await createContext.CommitAsync();
        }
        
        // Clean working directory
        var testFile = Path.Combine(_workingDir, "test.txt");
        if (_fileSystem.File.Exists(testFile))
            _fileSystem.File.Delete(testFile);
        
        // Apply layer (cache hit)
        using (var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var applyContext = await session.BeginLayerAsync(uniqueHash);
            await applyContext.CommitAsync();
        }
        
        // Clean up
        if (_fileSystem.File.Exists(testFile))
            _fileSystem.File.Delete(testFile);
    }

    [Benchmark]
    public async Task SessionCreation_Overhead()
    {
        // Measure just the overhead of creating and disposing sessions
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        // Session is automatically disposed
    }

    [Benchmark]
    public async Task LayerContext_CreationOverhead()
    {
        var uniqueHash = $"context-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        // Context is disposed without any operations
    }

    [Benchmark]
    public async Task ChangeDetection_Overhead()
    {
        var uniqueHash = $"change-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create some files to detect changes on
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "file1.txt"), "content1");
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "file2.txt"), "content2");
            _fileSystem.Directory.CreateDirectory(Path.Combine(_workingDir, "subdir"));
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "subdir", "file3.txt"), "content3");
        }
        
        // This will trigger change detection
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        foreach (var item in _fileSystem.Directory.EnumerateFileSystemEntries(_workingDir))
        {
            if (_fileSystem.File.Exists(item))
                _fileSystem.File.Delete(item);
            else
                _fileSystem.Directory.Delete(item, true);
        }
    }

    [Benchmark]
    public async Task TarOperations_WriteOverhead()
    {
        var uniqueHash = $"tar-write-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create files of varying sizes to measure TAR write overhead
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "small.txt"), "small");
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "medium.txt"), new string('M', 10000));
            await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "large.txt"), new string('L', 100000));
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        foreach (var file in _fileSystem.Directory.GetFiles(_workingDir))
        {
            _fileSystem.File.Delete(file);
        }
    }

    [Benchmark]
    public async Task TarOperations_ReadOverhead()
    {
        var readHash = $"tar-read-{Guid.NewGuid()}";
        
        // First create a layer to read
        using (var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var context = await session.BeginLayerAsync(readHash);
            if (!context.IsFromCache)
            {
                await _fileSystem.File.WriteAllTextAsync(Path.Combine(_workingDir, "test.txt"), "test content");
            }
            await context.CommitAsync();
            
            var testFile = Path.Combine(_workingDir, "test.txt");
            if (_fileSystem.File.Exists(testFile))
                _fileSystem.File.Delete(testFile);
        }
        
        // Now measure the read overhead (cache hit)
        using (var readSession = await LayerFileSystem.StartSession(_workingDir, _cacheDir))
        {
            using var readContext = await readSession.BeginLayerAsync(readHash);
            await readContext.CommitAsync();
        }
        
        // Clean up
        var cleanupFile = Path.Combine(_workingDir, "test.txt");
        if (_fileSystem.File.Exists(cleanupFile))
            _fileSystem.File.Delete(cleanupFile);
    }

    [Benchmark]
    public async Task PathNormalization_Overhead()
    {
        var uniqueHash = $"path-norm-{Guid.NewGuid()}";
        using var session = await LayerFileSystem.StartSession(_workingDir, _cacheDir);
        using var context = await session.BeginLayerAsync(uniqueHash);
        
        if (!context.IsFromCache)
        {
            // Create files with various path patterns that require normalization
            var paths = new[]
            {
                "file1.txt",
                "sub/file2.txt",
                "Sub/File3.txt", // Case differences
                "deep/nested/path/file4.txt",
                "path with spaces/file5.txt"
            };
            
            foreach (var relativePath in paths)
            {
                var fullPath = Path.Combine(_workingDir, relativePath);
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !_fileSystem.Directory.Exists(dir))
                {
                    _fileSystem.Directory.CreateDirectory(dir);
                }
                await _fileSystem.File.WriteAllTextAsync(fullPath, $"Content for {relativePath}");
            }
        }
        
        var layerInfo = await context.CommitAsync();
        
        // Clean up
        foreach (var item in _fileSystem.Directory.EnumerateFileSystemEntries(_workingDir))
        {
            if (_fileSystem.File.Exists(item))
                _fileSystem.File.Delete(item);
            else
                _fileSystem.Directory.Delete(item, true);
        }
    }
}