using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace LayeredFileSystem.Core;

public class ChangeDetector(IFileSystem fileSystem, IPathNormalizer pathNormalizer) : IChangeDetector
{
    public async Task<IReadOnlyList<FileChange>> DetectChangesAsync(
        DirectorySnapshot before, 
        DirectorySnapshot after)
    {
        var changes = new List<FileChange>();

        // Find added and modified files
        foreach (var (path, afterMetadata) in after.Files)
        {
            var normalizedPath = pathNormalizer.NormalizePath(path);
            
            if (!before.Files.TryGetValue(normalizedPath, out var beforeMetadata))
            {
                // File was added
                changes.Add(new FileChange
                {
                    RelativePath = normalizedPath,
                    Type = ChangeType.Added,
                    FileInfo = afterMetadata.IsDirectory ? null : new FileInfo(afterMetadata.RelativePath)
                });
            }
            else if (!AreMetadataEqual(beforeMetadata, afterMetadata))
            {
                // File was modified
                changes.Add(new FileChange
                {
                    RelativePath = normalizedPath,
                    Type = ChangeType.Modified,
                    FileInfo = afterMetadata.IsDirectory ? null : new FileInfo(afterMetadata.RelativePath)
                });
            }
        }

        // Find deleted files
        foreach (var (path, beforeMetadata) in before.Files)
        {
            var normalizedPath = pathNormalizer.NormalizePath(path);
            
            if (!after.Files.ContainsKey(normalizedPath))
            {
                // File was deleted
                changes.Add(new FileChange
                {
                    RelativePath = normalizedPath,
                    Type = ChangeType.Deleted,
                    FileInfo = null
                });
            }
        }

        return changes;
    }

    public async Task<DirectorySnapshot> CreateSnapshotAsync(string directoryPath)
    {
        var snapshot = new DirectorySnapshot();
        
        if (!fileSystem.Directory.Exists(directoryPath))
        {
            return snapshot;
        }

        await ScanDirectoryAsync(directoryPath, directoryPath, snapshot);
        return snapshot;
    }

    private async Task ScanDirectoryAsync(string rootPath, string currentPath, DirectorySnapshot snapshot)
    {
        try
        {
            // Add directory entry
            var relativePath = fileSystem.Path.GetRelativePath(rootPath, currentPath);
            if (!string.IsNullOrEmpty(relativePath) && relativePath != ".")
            {
                var normalizedPath = pathNormalizer.NormalizePath(relativePath);
                snapshot.Files[normalizedPath] = new FileMetadata
                {
                    RelativePath = normalizedPath,
                    Size = 0,
                    LastWriteTime = fileSystem.Directory.GetLastWriteTime(currentPath),
                    Hash = string.Empty,
                    IsDirectory = true
                };
            }

            // Process files
            foreach (var filePath in fileSystem.Directory.GetFiles(currentPath))
            {
                var fileRelativePath = fileSystem.Path.GetRelativePath(rootPath, filePath);
                var normalizedPath = pathNormalizer.NormalizePath(fileRelativePath);
                
                var fileInfo = fileSystem.FileInfo.New(filePath);
                var hash = await CalculateFileHashAsync(filePath);
                
                snapshot.Files[normalizedPath] = new FileMetadata
                {
                    RelativePath = normalizedPath,
                    Size = fileInfo.Length,
                    LastWriteTime = fileInfo.LastWriteTime,
                    Hash = hash,
                    IsDirectory = false
                };
            }

            // Process subdirectories
            foreach (var subdirectoryPath in fileSystem.Directory.GetDirectories(currentPath))
            {
                await ScanDirectoryAsync(rootPath, subdirectoryPath, snapshot);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Skip directories that were deleted during scan
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        try
        {
            using var stream = fileSystem.File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hashBytes);
        }
        catch
        {
            // If we can't read the file, return a hash based on metadata
            var fileInfo = fileSystem.FileInfo.New(filePath);
            var metadata = $"{fileInfo.Length}:{fileInfo.LastWriteTime:O}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(metadata));
            return Convert.ToBase64String(hashBytes);
        }
    }

    private static bool AreMetadataEqual(FileMetadata before, FileMetadata after)
    {
        return before.Size == after.Size &&
               before.LastWriteTime == after.LastWriteTime &&
               before.Hash == after.Hash &&
               before.IsDirectory == after.IsDirectory;
    }
}