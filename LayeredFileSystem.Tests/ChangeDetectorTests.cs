using System.IO.Abstractions.TestingHelpers;
using LayeredFileSystem.Core;

namespace LayeredFileSystem.Tests;

public class ChangeDetectorTests
{
    [Fact]
    public async Task DetectChangesAsync_WithAddedFile_ShouldDetectAddition()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var pathNormalizer = new PathNormalizer(fileSystem);
        var detector = new ChangeDetector(fileSystem, pathNormalizer);

        var before = new DirectorySnapshot();
        var after = new DirectorySnapshot
        {
            Files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["test.txt"] = new FileMetadata
                {
                    RelativePath = "test.txt",
                    Size = 100,
                    LastWriteTime = DateTime.UtcNow,
                    Hash = "hash123",
                    IsDirectory = false
                }
            }
        };

        // Act
        var changes = await detector.DetectChangesAsync(before, after);

        // Assert
        Assert.Single(changes);
        Assert.Equal(ChangeType.Added, changes[0].Type);
        Assert.Equal("test.txt", changes[0].RelativePath);
    }

    [Fact]
    public async Task DetectChangesAsync_WithDeletedFile_ShouldDetectDeletion()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var pathNormalizer = new PathNormalizer(fileSystem);
        var detector = new ChangeDetector(fileSystem, pathNormalizer);

        var before = new DirectorySnapshot
        {
            Files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["test.txt"] = new FileMetadata
                {
                    RelativePath = "test.txt",
                    Size = 100,
                    LastWriteTime = DateTime.UtcNow,
                    Hash = "hash123",
                    IsDirectory = false
                }
            }
        };
        var after = new DirectorySnapshot();

        // Act
        var changes = await detector.DetectChangesAsync(before, after);

        // Assert
        Assert.Single(changes);
        Assert.Equal(ChangeType.Deleted, changes[0].Type);
        Assert.Equal("test.txt", changes[0].RelativePath);
    }

    [Fact]
    public async Task DetectChangesAsync_WithModifiedFile_ShouldDetectModification()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var pathNormalizer = new PathNormalizer(fileSystem);
        var detector = new ChangeDetector(fileSystem, pathNormalizer);

        var before = new DirectorySnapshot
        {
            Files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["test.txt"] = new FileMetadata
                {
                    RelativePath = "test.txt",
                    Size = 100,
                    LastWriteTime = DateTime.UtcNow.AddMinutes(-1),
                    Hash = "hash123",
                    IsDirectory = false
                }
            }
        };
        var after = new DirectorySnapshot
        {
            Files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["test.txt"] = new FileMetadata
                {
                    RelativePath = "test.txt",
                    Size = 200,
                    LastWriteTime = DateTime.UtcNow,
                    Hash = "hash456",
                    IsDirectory = false
                }
            }
        };

        // Act
        var changes = await detector.DetectChangesAsync(before, after);

        // Assert
        Assert.Single(changes);
        Assert.Equal(ChangeType.Modified, changes[0].Type);
        Assert.Equal("test.txt", changes[0].RelativePath);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithNonExistentDirectory_ShouldReturnEmptySnapshot()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var pathNormalizer = new PathNormalizer(fileSystem);
        var detector = new ChangeDetector(fileSystem, pathNormalizer);

        // Act
        var snapshot = await detector.CreateSnapshotAsync("/nonexistent");

        // Assert
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Files);
    }
}