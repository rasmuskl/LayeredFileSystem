using System.IO.Abstractions.TestingHelpers;
using LayeredFileSystem.Core;

namespace LayeredFileSystem.Tests;

public class LayeredFileSystemTests
{
    [Fact]
    public async Task CreateSessionAsync_WithEmptyWorkingDirectory_ShouldSucceed()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var workingDir = "/tmp/work";
        var cacheDir = "/tmp/cache";
        
        fileSystem.Directory.CreateDirectory(workingDir);
        fileSystem.Directory.CreateDirectory(cacheDir);
        
        var layeredFs = new Core.LayeredFileSystem(fileSystem);

        // Act
        using var session = await layeredFs.CreateSessionAsync(workingDir, cacheDir);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(workingDir, session.WorkingDirectory);
        Assert.Empty(session.AppliedLayers);
    }

    [Fact]
    public async Task CreateSessionAsync_WithNonEmptyWorkingDirectory_ShouldThrow()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var workingDir = "/tmp/work";
        var cacheDir = "/tmp/cache";
        
        fileSystem.Directory.CreateDirectory(workingDir);
        fileSystem.Directory.CreateDirectory(cacheDir);
        fileSystem.File.WriteAllText($"{workingDir}/existing.txt", "content");
        
        var layeredFs = new Core.LayeredFileSystem(fileSystem);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LayeredFileSystemException>(
            () => layeredFs.CreateSessionAsync(workingDir, cacheDir));
        
        Assert.Contains("must be empty", exception.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_WithInvalidArguments_ShouldThrow()
    {
        // Arrange
        var layeredFs = new Core.LayeredFileSystem();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => layeredFs.CreateSessionAsync("", "/tmp/cache"));
        
        await Assert.ThrowsAsync<ArgumentException>(
            () => layeredFs.CreateSessionAsync("/tmp/work", ""));
    }
}