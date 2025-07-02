# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET library implementing a Docker-like layered file system using TAR archives for layer storage. The library provides an imperative API for creating, caching, and applying file system layers with cross-platform compatibility.

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

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test LayeredFileSystem.Tests/
```

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
- Test Framework: xUnit (when implemented)
- All APIs are async with CancellationToken support
- Nullable reference types enabled