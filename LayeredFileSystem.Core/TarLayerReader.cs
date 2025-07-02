using System.Formats.Tar;
using System.IO.Abstractions;

namespace LayeredFileSystem.Core;

public class TarLayerReader(IFileSystem fileSystem, IPathNormalizer pathNormalizer) : ITarLayerReader
{
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
        var normalizedPath = pathNormalizer.NormalizePath(entry.Name);
        var targetPath = fileSystem.Path.Combine(targetDirectory, normalizedPath);

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
        var fileName = fileSystem.Path.GetFileName(path);
        return fileName.StartsWith(".wh.");
    }

    private async Task ProcessWhiteoutFileAsync(string whiteoutPath, string targetDirectory)
    {
        var fileName = fileSystem.Path.GetFileName(whiteoutPath);
        
        if (fileName == ".wh..wh..opq")
        {
            // This is an opaque whiteout - remove the entire directory
            var directoryPath = fileSystem.Path.GetDirectoryName(whiteoutPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                var targetPath = fileSystem.Path.Combine(targetDirectory, directoryPath);
                if (fileSystem.Directory.Exists(targetPath))
                {
                    fileSystem.Directory.Delete(targetPath, recursive: true);
                }
            }
        }
        else if (fileName.StartsWith(".wh."))
        {
            // This is a regular whiteout - remove the specific file/directory
            var originalFileName = fileName.Substring(4); // Remove ".wh." prefix
            var directoryPath = fileSystem.Path.GetDirectoryName(whiteoutPath) ?? string.Empty;
            var originalPath = string.IsNullOrEmpty(directoryPath)
                ? originalFileName
                : fileSystem.Path.Combine(directoryPath, originalFileName);
            
            var targetPath = fileSystem.Path.Combine(targetDirectory, originalPath);
            
            if (fileSystem.File.Exists(targetPath))
            {
                fileSystem.File.Delete(targetPath);
            }
            else if (fileSystem.Directory.Exists(targetPath))
            {
                fileSystem.Directory.Delete(targetPath, recursive: true);
            }
        }
    }

    private async Task CreateDirectoryAsync(string targetPath)
    {
        if (!fileSystem.Directory.Exists(targetPath))
        {
            fileSystem.Directory.CreateDirectory(targetPath);
        }
    }

    private async Task CreateFileAsync(TarEntry entry, string targetPath)
    {
        // Ensure the parent directory exists
        var directoryPath = fileSystem.Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directoryPath) && !fileSystem.Directory.Exists(directoryPath))
        {
            fileSystem.Directory.CreateDirectory(directoryPath);
        }

        // Create the file
        using var fileStream = fileSystem.File.Create(targetPath);
        if (entry.DataStream != null)
        {
            await entry.DataStream.CopyToAsync(fileStream);
        }
    }
}