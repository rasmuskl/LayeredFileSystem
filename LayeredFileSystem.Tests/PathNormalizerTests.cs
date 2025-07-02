using System.IO.Abstractions.TestingHelpers;
using LayeredFileSystem.Core;

namespace LayeredFileSystem.Tests;

public class PathNormalizerTests
{
    [Theory]
    [InlineData("path/to/file", "path/to/file")]
    [InlineData("path\\to\\file", "path/to/file")]
    [InlineData("path//to//file", "path/to/file")]
    [InlineData("path\\\\to\\\\file", "path/to/file")]
    [InlineData("/path/to/file", "path/to/file")]
    [InlineData("path/to/file/", "path/to/file")]
    [InlineData("", "")]
    [InlineData("/", "")]
    public void NormalizePath_ShouldNormalizeCorrectly(string input, string expected)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var normalizer = new PathNormalizer(fileSystem);

        // Act
        var result = normalizer.NormalizePath(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HasDuplicate_WithCaseInsensitiveDuplicates_ShouldReturnTrue()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var normalizer = new PathNormalizer(fileSystem);
        var existingPaths = new[] { "Path/To/File.txt", "another/path" };

        // Act
        var result = normalizer.HasDuplicate("path/to/file.txt", existingPaths);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasDuplicate_WithNoDuplicates_ShouldReturnFalse()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var normalizer = new PathNormalizer(fileSystem);
        var existingPaths = new[] { "Path/To/Different.txt", "another/path" };

        // Act
        var result = normalizer.HasDuplicate("path/to/file.txt", existingPaths);

        // Assert
        Assert.False(result);
    }
}