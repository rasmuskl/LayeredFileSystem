# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **complete** .NET library implementing a Docker-like layered file system using TAR archives for layer storage. The library provides an imperative API for creating, caching, and applying file system layers with full cross-platform compatibility.

## Implementation Status

✅ **COMPLETE** - All core functionality implemented and tested:
- All spec interfaces and models implemented
- Comprehensive unit and integration tests (28 tests)
- Cross-platform compatibility verified (Windows + Linux)
- Sample project demonstrating usage
- Full documentation and README

## Build Commands

```bash
# Build the solution
dotnet build LayeredFileSystem.sln

# Build in release mode
dotnet build LayeredFileSystem.sln -c Release

# Restore packages
dotnet restore

# Clean build artifacts
dotnet clean
```

## Testing Commands

### Standard Testing (Linux/WSL)
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test LayeredFileSystem.Tests/

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Cross-Platform Testing (Windows from WSL)
```bash
# Run tests on Windows .NET from WSL (for cross-platform verification)
powershell.exe -Command "cd 'C:\dev\experiments\LayeredFileSystem'; dotnet test --verbosity normal"

# Run tests with detailed output on Windows
powershell.exe -Command "cd 'C:\dev\experiments\LayeredFileSystem'; dotnet test --logger 'console;verbosity=detailed'"
```

### Test Coverage
- **Total Tests**: 28 (all passing on both Linux and Windows)
- **Unit Tests**: PathNormalizer, ChangeDetector, LayeredFileSystem, TestFileSystemBuilder
- **Integration Tests**: Full workflows, caching, whiteout files, large files, rollback
- **Cross-Platform**: Verified on Windows and Linux

## Architecture

The library implements a layered file system using these core components:

### Core Interfaces
- **ILayeredFileSystem**: Main entry point for creating sessions
- **ILayerSession**: Manages layer operations in a working directory with cache
- **ILayerContext**: Handles individual layer creation/application
- **IChangeDetector**: Detects file system changes between snapshots
- **ITarLayerWriter/Reader**: TAR archive operations for layers
- **ILayerCache**: Layer caching with hash-based storage
- **IPathNormalizer**: Cross-platform path handling with case-insensitive support

### Key Concepts
- **Empty Directory Starting Point**: All operations begin with an empty working directory
- **Step-Based Workflow**: Each step involves cache lookup, file operations, and change snapshots
- **Docker-style Whiteout Files**: Uses `.wh.` prefix files to represent deletions in TAR layers
- **Case-Insensitive Paths**: Ensures consistent behavior across Windows/Linux/macOS
- **Hash-Based Caching**: Layers stored as TAR files with hash-based naming

### TAR Layer Format
- Uses System.Formats.Tar with PAX format
- Supports streaming operations for large files
- Implements Docker-style deletion tracking with whiteout files
- Uses `.wh..wh..opq` files for entire directory deletions

### Error Handling
Custom exception hierarchy including:
- `LayeredFileSystemException` (base)
- `DuplicatePathException` (case-insensitive path conflicts)
- `LayerNotFoundException` (missing cached layers)

## Framework and Dependencies

- Target Framework: .NET 9.0
- Key Dependencies: System.Formats.Tar, System.IO.Abstractions
- Test Framework: xUnit with System.IO.Abstractions.TestingHelpers
- All APIs are async with CancellationToken support
- Nullable reference types enabled

## Sample Usage

```bash
# Run the sample application
dotnet run --project LayeredFileSystem.Sample
```

The sample demonstrates:
- Creating layered file systems with caching
- Layer creation and application
- Cache hit/miss scenarios across sessions
- Proper resource disposal

## Benchmark Commands

```bash
# Run all benchmarks
dotnet run --project LayeredFileSystem.Benchmarks -c Release

# Run cache performance benchmarks only
dotnet run --project LayeredFileSystem.Benchmarks -c Release cache

# Run system overhead benchmarks only
dotnet run --project LayeredFileSystem.Benchmarks -c Release overhead

# Build benchmarks project
dotnet build LayeredFileSystem.Benchmarks -c Release
```

### Benchmark Suites

**Cache Benchmarks** - Measure cache performance benefits:
- Cache hit vs miss scenarios
- Large file operations
- Multiple file operations
- Layer creation and application workflows

**Overhead Benchmarks** - Measure system overhead:
- Direct file operations baseline
- Layered system overhead comparison
- Session creation overhead
- Change detection overhead
- TAR read/write overhead
- Path normalization overhead

## Known Working Platforms

- ✅ **Linux** (Ubuntu on WSL2)
- ✅ **Windows** (.NET 9.0)
- ✅ **Cross-platform** path handling verified

## Development Notes

- Use `todo.md` for tracking remaining tasks and improvements
- Path normalization uses forward slashes for platform consistency
- TAR layers use PAX format for maximum compatibility
- All file operations use System.IO.Abstractions for testability
- Comprehensive integration tests cover real-world scenarios