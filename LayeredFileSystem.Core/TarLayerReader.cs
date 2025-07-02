using System.Formats.Tar;
using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class TarLayerReader : ITarLayerReader
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathNormalizer _pathNormalizer;

    public TarLayerReader(IFileSystem fileSystem, IPathNormalizer pathNormalizer)
    {
        _fileSystem = fileSystem;
        _pathNormalizer = pathNormalizer;
    }

    public async Task ApplyLayerAsync(Stream layerStream, string targetDirectory)
    {
        using var reader = new TarReader(layerStream, leaveOpen: true);
        
        TarEntry? entry;
        while ((entry = await reader.GetNextEntryAsync()) != null)
        {
            await ProcessTarEntryAsync(entry, targetDirectory);
        }
    }

    private async Task ProcessTarEntryAsync(TarEntry entry, string targetDirectory)
    {
        var normalizedPath = _pathNormalizer.NormalizePath(entry.Name);
        var targetPath = _fileSystem.Path.Combine(targetDirectory, normalizedPath);

        // Handle whiteout files (deletions)
        if (IsWhiteoutFile(normalizedPath))
        {
            await ProcessWhiteoutFileAsync(normalizedPath, targetDirectory);
            return;
        }

        // Handle regular files and directories
        switch (entry.EntryType)
        {
            case TarEntryType.Directory:
                await CreateDirectoryAsync(targetPath);
                break;
                
            case TarEntryType.RegularFile:
                await CreateFileAsync(entry, targetPath);
                break;
                
            default:
                // Skip other entry types (symlinks, etc.)
                break;
        }
    }

    private bool IsWhiteoutFile(string path)
    {
        var fileName = _fileSystem.Path.GetFileName(path);
        return fileName.StartsWith(".wh.");
    }

    private async Task ProcessWhiteoutFileAsync(string whiteoutPath, string targetDirectory)
    {
        var fileName = _fileSystem.Path.GetFileName(whiteoutPath);
        
        if (fileName == ".wh..wh..opq")
        {
            // This is an opaque whiteout - remove the entire directory
            var directoryPath = _fileSystem.Path.GetDirectoryName(whiteoutPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                var targetPath = _fileSystem.Path.Combine(targetDirectory, directoryPath);
                if (_fileSystem.Directory.Exists(targetPath))
                {
                    _fileSystem.Directory.Delete(targetPath, recursive: true);
                }
            }
        }
        else if (fileName.StartsWith(".wh."))
        {
            // This is a regular whiteout - remove the specific file/directory
            var originalFileName = fileName.Substring(4); // Remove ".wh." prefix
            var directoryPath = _fileSystem.Path.GetDirectoryName(whiteoutPath) ?? string.Empty;
            var originalPath = string.IsNullOrEmpty(directoryPath)
                ? originalFileName
                : _fileSystem.Path.Combine(directoryPath, originalFileName);
            
            var targetPath = _fileSystem.Path.Combine(targetDirectory, originalPath);
            
            if (_fileSystem.File.Exists(targetPath))
            {
                _fileSystem.File.Delete(targetPath);
            }
            else if (_fileSystem.Directory.Exists(targetPath))
            {
                _fileSystem.Directory.Delete(targetPath, recursive: true);
            }
        }
    }

    private async Task CreateDirectoryAsync(string targetPath)
    {
        if (!_fileSystem.Directory.Exists(targetPath))
        {
            _fileSystem.Directory.CreateDirectory(targetPath);
        }
    }

    private async Task CreateFileAsync(TarEntry entry, string targetPath)
    {
        // Ensure the parent directory exists
        var directoryPath = _fileSystem.Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directoryPath) && !_fileSystem.Directory.Exists(directoryPath))
        {
            _fileSystem.Directory.CreateDirectory(directoryPath);
        }

        // Create the file
        using var fileStream = _fileSystem.File.Create(targetPath);
        if (entry.DataStream != null)
        {
            await entry.DataStream.CopyToAsync(fileStream);
        }
    }
}