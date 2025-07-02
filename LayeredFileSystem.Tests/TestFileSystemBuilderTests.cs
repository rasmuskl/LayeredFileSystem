using LayeredFileSystem.Core;

namespace LayeredFileSystem.Tests;

public class TestFileSystemBuilderTests
{
    [Fact]
    public async Task TestFileSystemBuilder_BasicOperations_ShouldWork()
    {
        // Arrange & Act
        var builder = new TestFileSystemBuilder()
            .WithFile("config.json", "{\"name\": \"test\"}")
            .WithDirectory("src")
            .WithFile("src/main.cs", "Console.WriteLine(\"Hello\");");

        var snapshot = await builder.BuildSnapshotAsync();

        // Assert
        Assert.Contains("config.json", snapshot.Files.Keys);
        Assert.Contains("src", snapshot.Files.Keys);
        Assert.Contains("src/main.cs", snapshot.Files.Keys);
        Assert.True(snapshot.Files["src"].IsDirectory);
        Assert.False(snapshot.Files["config.json"].IsDirectory);
    }

    [Fact]
    public async Task TestFileSystemBuilder_WithLayeredFileSystem_ShouldWork()
    {
        // Arrange
        var builder = new TestFileSystemBuilder();
        var layeredFs = builder.CreateLayeredFileSystem();
        
        // Use an empty working directory for layered file system
        var workingDir = "/empty-work";
        var cacheDir = "/cache";
        builder.GetFileSystem().Directory.CreateDirectory(workingDir);
        builder.GetFileSystem().Directory.CreateDirectory(cacheDir);

        // Act
        using var session = await layeredFs.CreateSessionAsync(workingDir, cacheDir);
        
        // The working directory should start empty for layered file system
        Assert.Empty(session.AppliedLayers);
        
        using (var layer = await session.BeginLayerAsync("test-layer"))
        {
            // Add files directly to the working directory
            builder.GetFileSystem().File.WriteAllText("/empty-work/newfile.txt", "new content");
            var layerInfo = await layer.CommitAsync();
            
            // Should have detected the new file
            Assert.Equal(1, layerInfo.Statistics.FilesAdded);
        }
    }

    [Fact]
    public async Task TestFileSystemBuilder_FileOperations_ShouldWork()
    {
        // Arrange
        var builder = new TestFileSystemBuilder()
            .WithFile("test.txt", "original content")
            .WithFile("delete-me.txt", "to be deleted");

        // Act - Modify and delete
        builder
            .ModifyFile("test.txt", "modified content")
            .DeleteFile("delete-me.txt");

        // Assert
        var fileSystem = builder.GetFileSystem();
        Assert.True(fileSystem.File.Exists("/temp/test.txt"));
        Assert.False(fileSystem.File.Exists("/temp/delete-me.txt"));
        Assert.Equal("modified content", fileSystem.File.ReadAllText("/temp/test.txt"));
    }

    [Fact]
    public void TestFileSystemBuilder_ListAllFiles_ShouldReturnAllFiles()
    {
        // Arrange & Act
        var builder = new TestFileSystemBuilder()
            .WithCommonStructure()
            .WithFile("extra.txt", "extra content");

        var allFiles = builder.ListAllFiles().ToList();

        // Assert
        Assert.Contains(allFiles, f => f.EndsWith("README.md"));
        Assert.Contains(allFiles, f => f.EndsWith("main.cs"));
        Assert.Contains(allFiles, f => f.EndsWith("test1.cs"));
        Assert.Contains(allFiles, f => f.EndsWith("extra.txt"));
        Assert.True(allFiles.Count >= 4);
    }
}