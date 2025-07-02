using System.Formats.Tar;
using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class TarLayerWriter : ITarLayerWriter
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathNormalizer _pathNormalizer;

    public TarLayerWriter(IFileSystem fileSystem, IPathNormalizer pathNormalizer)
    {
        _fileSystem = fileSystem;
        _pathNormalizer = pathNormalizer;
    }

    public async Task CreateLayerAsync(
        IReadOnlyList<FileChange> changes,
        string baseDirectory,
        Stream outputStream)
    {
        using var writer = new TarWriter(outputStream, TarEntryFormat.Pax, leaveOpen: true);

        foreach (var change in changes)
        {
            await ProcessChangeAsync(writer, change, baseDirectory);
        }
    }

    private async Task ProcessChangeAsync(TarWriter writer, FileChange change, string baseDirectory)
    {
        var normalizedPath = _pathNormalizer.NormalizePath(change.RelativePath);
        
        switch (change.Type)
        {
            case ChangeType.Added:
            case ChangeType.Modified:
                await AddFileToTarAsync(writer, normalizedPath, baseDirectory);
                break;
                
            case ChangeType.Deleted:
                await AddWhiteoutFileAsync(writer, normalizedPath);
                break;
        }
    }

    private async Task AddFileToTarAsync(TarWriter writer, string relativePath, string baseDirectory)
    {
        var fullPath = _fileSystem.Path.Combine(baseDirectory, relativePath);
        
        if (!_fileSystem.File.Exists(fullPath) && !_fileSystem.Directory.Exists(fullPath))
        {
            return;
        }

        if (_fileSystem.Directory.Exists(fullPath))
        {
            // Add directory entry
            var dirEntry = new PaxTarEntry(TarEntryType.Directory, relativePath);
            await writer.WriteEntryAsync(dirEntry);
        }
        else
        {
            // Add file entry
            var fileInfo = _fileSystem.FileInfo.New(fullPath);
            using var fileStream = _fileSystem.File.OpenRead(fullPath);
            var fileEntry = new PaxTarEntry(TarEntryType.RegularFile, relativePath)
            {
                DataStream = fileStream
            };
            
            await writer.WriteEntryAsync(fileEntry);
        }
    }

    private async Task AddWhiteoutFileAsync(TarWriter writer, string relativePath)
    {
        var directoryPath = _fileSystem.Path.GetDirectoryName(relativePath) ?? string.Empty;
        var fileName = _fileSystem.Path.GetFileName(relativePath);
        
        // Check if this is a directory deletion
        if (IsDirectoryDeletion(relativePath))
        {
            // Use .wh..wh..opq for directory deletions
            var opqPath = _fileSystem.Path.Combine(relativePath, ".wh..wh..opq");
            using var emptyStream = new MemoryStream();
            var opqEntry = new PaxTarEntry(TarEntryType.RegularFile, opqPath)
            {
                DataStream = emptyStream
            };
            await writer.WriteEntryAsync(opqEntry);
        }
        else
        {
            // Use .wh.<filename> for file deletions
            var whiteoutFileName = $".wh.{fileName}";
            var whiteoutPath = string.IsNullOrEmpty(directoryPath) 
                ? whiteoutFileName 
                : _fileSystem.Path.Combine(directoryPath, whiteoutFileName);
            
            using var emptyStream = new MemoryStream();
            var whiteoutEntry = new PaxTarEntry(TarEntryType.RegularFile, whiteoutPath)
            {
                DataStream = emptyStream
            };
            await writer.WriteEntryAsync(whiteoutEntry);
        }
    }

    private bool IsDirectoryDeletion(string relativePath)
    {
        // This is a simplified check - in a real implementation, you might track
        // whether the original path was a directory
        return !_fileSystem.Path.HasExtension(relativePath);
    }
}