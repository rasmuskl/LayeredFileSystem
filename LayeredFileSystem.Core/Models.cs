namespace LayeredFileSystem.Core;

public class LayerInfo
{
    public string Hash { get; set; } = string.Empty;
    public string LayerPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public LayerStatistics Statistics { get; set; } = new();
}

public class LayerStatistics
{
    public int FilesAdded { get; set; }
    public int FilesModified { get; set; }
    public int FilesDeleted { get; set; }
    public int DirectoriesAdded { get; set; }
    public int DirectoriesDeleted { get; set; }
}

public class FileChange
{
    public string RelativePath { get; set; } = string.Empty;
    public ChangeType Type { get; set; }
    public FileInfo? FileInfo { get; set; }
}

public enum ChangeType
{
    Added,
    Modified,
    Deleted
}

public class DirectorySnapshot
{
    public Dictionary<string, FileMetadata> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class FileMetadata
{
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
}

public class CacheStatistics
{
    public long TotalSizeBytes { get; set; }
    public int LayerCount { get; set; }
    public DateTime LastAccessed { get; set; }
}