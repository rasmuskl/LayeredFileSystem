# LayeredFileSystem

A .NET 9.0 library implementing a Docker-like layered file system using TAR archives for layer storage. The library provides an imperative API for creating, caching, and applying file system layers with cross-platform compatibility.

## Features

- **Docker-style Layer Management**: Create and apply file system layers similar to Docker images
- **TAR-based Storage**: Uses System.Formats.Tar with PAX format for maximum compatibility
- **Smart Caching**: Hash-based layer caching with automatic cache lookup
- **Change Detection**: Efficient tracking of file additions, modifications, and deletions
- **Whiteout Files**: Docker-compatible deletion tracking using `.wh.` prefix files
- **Cross-Platform**: Consistent behavior across Windows, Linux, and macOS
- **Case-Insensitive Paths**: Handles case-insensitive file systems correctly
- **Streaming Operations**: Memory-efficient handling of large files

## Quick Start

```csharp
using LayeredFileSystem.Core;

var fileSystem = new LayeredFileSystem();
using var session = await fileSystem.CreateSessionAsync(
    workingDirectory: "/tmp/build", 
    cacheDirectory: "/tmp/cache"
);

// Step 1: Initial setup
using (var layer1 = await session.BeginLayerAsync("setup-base-v1"))
{
    if (!layer1.IsFromCache)
    {
        // Perform file operations directly in working directory
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "config.json"), "{}");
        Directory.CreateDirectory(Path.Combine(session.WorkingDirectory, "src"));
    }
    var layerInfo = await layer1.CommitAsync();
    Console.WriteLine($"Layer created: {layerInfo.Statistics.FilesAdded} files, {layerInfo.SizeBytes} bytes");
}

// Step 2: Add source files (builds on previous layer)
using (var layer2 = await session.BeginLayerAsync("add-source-v1"))
{
    if (!layer2.IsFromCache)
    {
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "src/main.cs"), "// Main code");
        File.Delete(Path.Combine(session.WorkingDirectory, "config.json")); // Will create whiteout file
    }
    await layer2.CommitAsync();
}
```

## Architecture

### Core Components

- **ILayeredFileSystem**: Main entry point for creating sessions
- **ILayerSession**: Manages layer operations in a working directory with cache
- **ILayerContext**: Handles individual layer creation/application
- **IChangeDetector**: Detects file system changes between snapshots
- **ITarLayerWriter/Reader**: TAR archive operations for layers
- **ILayerCache**: Layer caching with hash-based storage
- **IPathNormalizer**: Cross-platform path handling

### Key Concepts

- **Empty Directory Starting Point**: All operations begin with an empty working directory
- **Step-Based Workflow**: Each step involves cache lookup, file operations, and change snapshots
- **Layer Composition**: Each layer builds on the previous state, similar to Docker layers
- **Efficient Caching**: Identical input hashes will reuse cached layers
- **Atomic Operations**: Layer commits are atomic - either succeed completely or fail

## TAR Layer Format

The library uses Docker-compatible TAR layer format:

- **Format**: PAX (POSIX.1-2001) for maximum compatibility
- **Whiteout Files**: Empty files with `.wh.` prefix mark deletions
- **Directory Deletion**: `.wh..wh..opq` files mark entire directory deletions
- **Streaming**: Supports large files without loading entirely into memory

## Error Handling

The library provides specific exception types:

```csharp
try
{
    var session = await fileSystem.CreateSessionAsync(workingDir, cacheDir);
}
catch (LayeredFileSystemException ex)
{
    // Base exception for all library errors
}
catch (DuplicatePathException ex)
{
    // Case-insensitive path conflicts
    Console.WriteLine($"Path conflict: {ex.Path} vs {ex.ExistingPath}");
}
catch (LayerNotFoundException ex)
{
    // Missing cached layer
    Console.WriteLine($"Layer not found: {ex.Hash}");
}
```

## Requirements

- .NET 9.0 or later
- System.Formats.Tar (built into .NET)
- System.IO.Abstractions (for testability)

## Dependencies

```xml
<PackageReference Include="System.IO.Abstractions" Version="21.1.3" />
```

## Building

```bash
# Build the solution
dotnet build

# Build in release mode
dotnet build -c Release

# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Testing

The library includes comprehensive unit tests using xUnit and System.IO.Abstractions.TestingHelpers for file system mocking.

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test LayeredFileSystem.Tests/
```

## Platform Support

- **Windows**: Full support with case-insensitive path handling
- **Linux**: Full support with case-sensitive paths
- **macOS**: Full support with case-insensitive path handling

## Performance Considerations

- **Streaming**: All TAR operations use streaming for memory efficiency
- **Parallel Processing**: Independent file operations can be processed in parallel
- **Efficient Diffing**: Uses file modification times and sizes for quick change detection
- **Smart Caching**: Avoids redundant layer creation through hash-based lookup

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]