using System.IO.Abstractions.TestingHelpers;
using LayeredFileSystem.Core;

namespace LayeredFileSystem.Tests;

/// <summary>
/// Utility class for building test file system states as specified in the requirements
/// </summary>
public class TestFileSystemBuilder
{
    private readonly MockFileSystem _fileSystem;
    private readonly string _tempDirectory;

    public TestFileSystemBuilder()
    {
        _fileSystem = new MockFileSystem();
        _tempDirectory = "/temp";
        _fileSystem.Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Add a file with specified content to the test file system
    /// </summary>
    public TestFileSystemBuilder WithFile(string path, string content)
    {
        var fullPath = GetFullPath(path);
        
        // Ensure parent directory exists
        var parentDir = _fileSystem.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentDir) && !_fileSystem.Directory.Exists(parentDir))
        {
            _fileSystem.Directory.CreateDirectory(parentDir);
        }
        
        _fileSystem.File.WriteAllText(fullPath, content);
        return this;
    }

    /// <summary>
    /// Add a directory to the test file system
    /// </summary>
    public TestFileSystemBuilder WithDirectory(string path)
    {
        var fullPath = GetFullPath(path);
        _fileSystem.Directory.CreateDirectory(fullPath);
        return this;
    }

    /// <summary>
    /// Delete a file from the test file system
    /// </summary>
    public TestFileSystemBuilder DeleteFile(string path)
    {
        var fullPath = GetFullPath(path);
        if (_fileSystem.File.Exists(fullPath))
        {
            _fileSystem.File.Delete(fullPath);
        }
        return this;
    }

    /// <summary>
    /// Delete a directory from the test file system
    /// </summary>
    public TestFileSystemBuilder DeleteDirectory(string path)
    {
        var fullPath = GetFullPath(path);
        if (_fileSystem.Directory.Exists(fullPath))
        {
            _fileSystem.Directory.Delete(fullPath, recursive: true);
        }
        return this;
    }

    /// <summary>
    /// Modify an existing file with new content
    /// </summary>
    public TestFileSystemBuilder ModifyFile(string path, string newContent)
    {
        var fullPath = GetFullPath(path);
        if (_fileSystem.File.Exists(fullPath))
        {
            _fileSystem.File.WriteAllText(fullPath, newContent);
        }
        return this;
    }

    /// <summary>
    /// Build a directory snapshot from the current state
    /// </summary>
    public async Task<DirectorySnapshot> BuildSnapshotAsync()
    {
        var pathNormalizer = new PathNormalizer(_fileSystem);
        var changeDetector = new ChangeDetector(_fileSystem, pathNormalizer);
        return await changeDetector.CreateSnapshotAsync(_tempDirectory);
    }

    /// <summary>
    /// Get the mock file system for advanced operations
    /// </summary>
    public MockFileSystem GetFileSystem() => _fileSystem;

    /// <summary>
    /// Get the temp directory path
    /// </summary>
    public string GetTempDirectory() => _tempDirectory;

    /// <summary>
    /// List all files in the file system for debugging
    /// </summary>
    public IEnumerable<string> ListAllFiles()
    {
        return _fileSystem.AllFiles.Where(f => f.StartsWith(_tempDirectory));
    }

    /// <summary>
    /// Create a layered file system instance using this mock file system
    /// </summary>
    public Core.LayeredFileSystem CreateLayeredFileSystem()
    {
        return new Core.LayeredFileSystem(_fileSystem);
    }

    /// <summary>
    /// Copy the current state to a real directory for testing
    /// </summary>
    public async Task CopyToRealDirectoryAsync(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        foreach (var mockFile in _fileSystem.AllFiles.Where(f => f.StartsWith(_tempDirectory)))
        {
            var relativePath = _fileSystem.Path.GetRelativePath(_tempDirectory, mockFile);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            
            var content = _fileSystem.File.ReadAllText(mockFile);
            await File.WriteAllTextAsync(targetPath, content);
        }
    }

    private string GetFullPath(string path)
    {
        if (_fileSystem.Path.IsPathRooted(path))
        {
            return path;
        }
        return _fileSystem.Path.Combine(_tempDirectory, path);
    }
}

/// <summary>
/// Extension methods for easier test writing
/// </summary>
public static class TestFileSystemBuilderExtensions
{
    /// <summary>
    /// Create a builder with common test file structure
    /// </summary>
    public static TestFileSystemBuilder WithCommonStructure(this TestFileSystemBuilder builder)
    {
        return builder
            .WithDirectory("src")
            .WithDirectory("tests")
            .WithFile("README.md", "# Test Project")
            .WithFile("src/main.cs", "// Main code")
            .WithFile("tests/test1.cs", "// Test code");
    }
}