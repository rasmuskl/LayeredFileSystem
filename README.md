# LayeredFileSystem

Ever wondered how Docker builds work so fast? Or how package managers can efficiently track and apply changes to your project? This library brings that same **layered file system** magic to your .NET applications.

**LayeredFileSystem** lets you build file systems incrementally - like creating snapshots that stack on top of each other. Perfect for build systems, package managers, development tools, or any application that needs to efficiently manage file changes over time.

## What is this for?

Think of it like **Git for file systems**, but optimized for build tools:

- 🏗️ **Build Systems**: Cache intermediate build steps, only rebuild what changed
- 📦 **Package Managers**: Apply packages as layers, easy rollback and updates  
- 🔧 **Development Tools**: Create isolated environments that share common base layers
- 🎮 **Game Modding**: Apply mods as layers without modifying original files
- 💾 **Backup Systems**: Incremental backups with deduplication
- 🐳 **Container-like Tools**: Build your own Docker-style layering system

## Real-world Example

Imagine a build system that caches each step:

```
Layer 1: Base dependencies (node_modules, packages) ← Cache this!
Layer 2: Source code compilation                     ← Cache this too!  
Layer 3: Test results and reports                    ← Only rebuild if tests change
Layer 4: Final packaging                             ← Fast rebuilds!
```

With LayeredFileSystem, if only your tests change, you skip layers 1-2 and start from the cached layer 3!

## Features

- **Docker-style Layer Management**: Create and apply file system layers similar to Docker images
- **TAR-based Storage**: Uses System.Formats.Tar with PAX format for maximum compatibility
- **Smart Caching**: Hash-based layer caching with automatic cache lookup
- **Change Detection**: Efficient tracking of file additions, modifications, and deletions
- **Whiteout Files**: Docker-compatible deletion tracking using `.wh.` prefix files
- **Cross-Platform**: Consistent behavior across Windows, Linux, and macOS
- **Case-Insensitive Paths**: Handles case-insensitive file systems correctly
- **Streaming Operations**: Memory-efficient handling of large files

## How it Works

LayeredFileSystem uses the same approach as Docker images:

1. **Start with an empty directory**
2. **Make changes** (add/modify/delete files)
3. **Create a layer** - captures only what changed
4. **Repeat** - each layer builds on the previous ones
5. **Cache everything** - identical layers are reused automatically

## Quick Start

```csharp
using LayeredFileSystem.Core;

// Simple entry point - no more complex instantiation!
using var session = await LayerFileSystem.StartSession(
    workingDirectory: "/tmp/build", 
    cacheDirectory: "/tmp/cache"
);

// Step 1: Set up base files
using (var layer1 = await session.BeginLayerAsync("setup-deps-v1"))
{
    if (!layer1.IsFromCache) // Only run if not cached
    {
        // Install dependencies, create config files, etc.
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "package.json"), "{}");
        Directory.CreateDirectory(Path.Combine(session.WorkingDirectory, "src"));
        
        // Simulate expensive setup work
        Console.WriteLine("Setting up dependencies...");
        await Task.Delay(1000); // This won't run next time!
    }
    await layer1.CommitAsync();
    Console.WriteLine($"Dependencies: {(layer1.IsFromCache ? "FROM CACHE ⚡" : "BUILT")}");
}

// Step 2: Compile source code  
using (var layer2 = await session.BeginLayerAsync("compile-v1"))
{
    if (!layer2.IsFromCache)
    {
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "src/Program.cs"), "Console.WriteLine(\"Hello!\");");
        // Run your compilation logic here
        Console.WriteLine("Compiling source...");
    }
    await layer2.CommitAsync();
    Console.WriteLine($"Compilation: {(layer2.IsFromCache ? "FROM CACHE ⚡" : "BUILT")}");
}
```

**First run output:**
```
Setting up dependencies...
Dependencies: BUILT
Compiling source...
Compilation: BUILT
```

**Second run output (same hash inputs):**
```
Dependencies: FROM CACHE ⚡
Compilation: FROM CACHE ⚡
```

That's the magic! Change your source code, and only the compilation layer rebuilds. The dependencies layer stays cached.

## Why choose LayeredFileSystem?

✅ **Battle-tested approach** - Same techniques Docker uses  
✅ **Pure .NET** - No external dependencies beyond .NET 9  
✅ **Cross-platform** - Windows, Linux, macOS  
✅ **Memory efficient** - Streams large files, doesn't load everything into RAM  
✅ **Production ready** - Comprehensive test suite with 28+ tests  
✅ **Easy to use** - Clean API, good documentation

## Common Use Cases

### Build System Caching
```csharp
// Cache expensive dependency resolution
var depsLayer = await session.BeginLayerAsync($"deps-{dependenciesHash}");
if (!depsLayer.IsFromCache) {
    await InstallDependencies(); // Only runs when dependencies change
}
await depsLayer.CommitAsync();
```

### Package Manager
```csharp
// Apply package as a layer
var packageLayer = await session.BeginLayerAsync($"pkg-{packageName}-{version}");
if (!packageLayer.IsFromCache) {
    await ExtractPackage(packageName, session.WorkingDirectory);
}
await packageLayer.CommitAsync();
```

### Development Environment
```csharp
// Base environment + project-specific tools
var baseLayer = await session.BeginLayerAsync("base-env-v1");
var projectLayer = await session.BeginLayerAsync($"project-{projectHash}");
// Now you have an isolated environment with shared base!
```

## Installation

### Option 1: NuGet Package (Coming Soon)
```bash
# Will be available once published to NuGet
dotnet add package LayeredFileSystem.Core
```

### Option 2: Build from Source
```bash
git clone https://github.com/rasmuskl/LayeredFileSystem
cd LayeredFileSystem
dotnet build
dotnet test  # Run the comprehensive test suite (28+ tests)
```

### Option 3: Create Package Locally
```bash
cd LayeredFileSystem.Core
dotnet pack --configuration Release
# Creates LayeredFileSystem.Core.1.0.0.nupkg in bin/Release/
```

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