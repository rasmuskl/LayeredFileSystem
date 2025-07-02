# Layered File System Library Specification

## Overview

Create a .NET library that implements a Docker-like layered file system using TAR archives for layer storage. The library should provide an imperative API for creating, caching, and applying file system layers with cross-platform compatibility.

## Core Requirements

### 1. Layer Management

- **Empty Directory Starting Point**: Operations begin with an empty working directory
- **Step-Based Workflow**: Each step consists of:
  1. Providing an input hash for cache lookup
  2. If cache hit: Apply cached TAR layer to directory
  3. If cache miss: Allow user to perform file operations, then snapshot changes
- **Snapshot Creation**: After each step, create a TAR archive containing only the changes (added/modified/deleted files)
- **Layer Storage**: Store layers as TAR files on the file system with hash-based naming

### 2. File System Operations

- **Case Sensitivity**: Use case-insensitive file paths with duplicate detection
- **Cross-Platform**: Ensure consistent behavior across Windows, Linux, and macOS
- **Change Detection**: Track file additions, modifications, and deletions between snapshots
- **Deletion Tracking**: Use Docker-style whiteout files (`.wh.` prefix) to represent deletions

### 3. TAR Archive Format

- **Library**: Use System.Formats.Tar (built into .NET)
- **Format**: PAX format for maximum compatibility
- **Streaming**: Support streaming operations for large files
- **Whiteout Files**: Empty files with `.wh.` prefix to mark deletions
- **Directory Deletion**: Use `.wh..wh..opq` files to mark entire directory deletions

## API Design

### Core Interfaces

```csharp
public interface ILayeredFileSystem
{
    /// <summary>
    /// Initialize a new layered file system session
    /// </summary>
    /// <param name="workingDirectory">Empty directory for operations</param>
    /// <param name="cacheDirectory">Directory for storing cached layers</param>
    Task<ILayerSession> CreateSessionAsync(string workingDirectory, string cacheDirectory);
}

public interface ILayerSession : IDisposable
{
    /// <summary>
    /// Begin a new layer step
    /// </summary>
    /// <param name="inputHash">Hash for cache lookup</param>
    /// <returns>Layer context for this step</returns>
    Task<ILayerContext> BeginLayerAsync(string inputHash);
    
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

public class LayerInfo
{
    public string Hash { get; set; }
    public string LayerPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public LayerStatistics Statistics { get; set; }
}

public class LayerStatistics
{
    public int FilesAdded { get; set; }
    public int FilesModified { get; set; }
    public int FilesDeleted { get; set; }
    public int DirectoriesAdded { get; set; }
    public int DirectoriesDeleted { get; set; }
}
```

### Usage Example

```csharp
// Clean, static factory method approach
using var session = await LayerFileSystem.StartSession(
    workingDirectory: "/tmp/build", 
    cacheDirectory: "/tmp/cache"
);

// Or use temporary directories for quick testing
using var tempSession = await LayerFileSystem.StartTemporarySession();

// Step 1: Initial setup
using (var layer1 = await session.BeginLayerAsync("setup-base-v1"))
{
    if (!layer1.IsFromCache)
    {
        // Perform file operations directly in working directory
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "config.json"), "{}");
        Directory.CreateDirectory(Path.Combine(session.WorkingDirectory, "src"));
    }
    await layer1.CommitAsync();
}

// Step 2: Add source files
using (var layer2 = await session.BeginLayerAsync("add-source-v1"))
{
    if (!layer2.IsFromCache)
    {
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "src/main.cs"), "...");
        File.Delete(Path.Combine(session.WorkingDirectory, "config.json"));
    }
    await layer2.CommitAsync();
}
```

## Implementation Requirements

### 1. Change Detection

```csharp
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

public class FileChange
{
    public string RelativePath { get; set; }
    public ChangeType Type { get; set; } // Added, Modified, Deleted
    public FileInfo? FileInfo { get; set; } // null for deletions
}

public class DirectorySnapshot
{
    public Dictionary<string, FileMetadata> Files { get; set; }
    // Case-insensitive path -> metadata mapping
}
```

### 2. TAR Layer Creation

```csharp
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
```

### 3. Cache Management

```csharp
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
```

### 4. Path Normalization

```csharp
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
```

## Error Handling

```csharp
public class LayeredFileSystemException : Exception
{
    public LayeredFileSystemException(string message) : base(message) { }
    public LayeredFileSystemException(string message, Exception inner) : base(message, inner) { }
}

public class DuplicatePathException : LayeredFileSystemException
{
    public string Path { get; }
    public string ExistingPath { get; }
    
    public DuplicatePathException(string path, string existingPath) 
        : base($"Path '{path}' conflicts with existing path '{existingPath}'")
    {
        Path = path;
        ExistingPath = existingPath;
    }
}

public class LayerNotFoundException : LayeredFileSystemException
{
    public string Hash { get; }
    
    public LayerNotFoundException(string hash) 
        : base($"Layer with hash '{hash}' not found in cache")
    {
        Hash = hash;
    }
}
```

## Testing Requirements

### 1. Integration Tests

Create comprehensive integration tests that:
- Test full layer creation and application workflows
- Verify whiteout file handling for deletions
- Test case-insensitive path handling
- Verify cross-platform behavior (run on Windows, Linux, macOS in CI)
- Test large file handling and streaming
- Verify cache hit/miss scenarios

### 2. Test Utilities

```csharp
public class TestFileSystemBuilder
{
    private readonly string _tempDirectory;
    
    public TestFileSystemBuilder WithFile(string path, string content);
    public TestFileSystemBuilder WithDirectory(string path);
    public TestFileSystemBuilder DeleteFile(string path);
    public DirectorySnapshot BuildSnapshot();
}
```

### 3. System.IO.Abstractions

Use System.IO.Abstractions for unit testing file system operations:
- Wrap all file system calls with IFileSystem interface
- Use MockFileSystem for unit tests
- Use real file system for integration tests

## Performance Considerations

1. **Streaming Operations**: Use async streaming for all TAR operations
2. **Buffer Sizes**: Use 64KB buffers for file I/O operations
3. **Parallel Processing**: Process independent file operations in parallel
4. **Lazy Loading**: Don't load entire TAR files into memory
5. **Efficient Diffing**: Use file modification times and sizes for quick change detection

## Platform-Specific Considerations

1. **Path Separators**: Always use Path.Combine and Path.DirectorySeparatorChar
2. **Line Endings**: Preserve original line endings in files
3. **File Permissions**: Ignore permissions for cross-platform compatibility
4. **Symbolic Links**: Skip symbolic links with warning
5. **Hidden Files**: Include hidden files in snapshots

## Deliverables

1. **Core Library**: LayeredFileSystem.Core
2. **Tests**: LayeredFileSystem.Tests (unit + integration)
3. **Sample**: LayeredFileSystem.Sample (example usage)

## Dependencies

- .NET 9.0
- System.Formats.Tar (built-in)
- System.IO.Abstractions (latest stable)
- System.IO.Abstractions.TestingHelpers (for tests)
- xUnit (for tests)

## Additional Notes

- All public APIs should be async
- Use CancellationToken support throughout
- Provide detailed XML documentation
- Follow .NET naming conventions
- Use nullable reference types
- Take advantage of .NET 9 features where beneficial (e.g., improved performance, new APIs)