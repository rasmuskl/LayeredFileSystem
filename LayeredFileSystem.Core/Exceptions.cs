namespace LayeredFileSystem.Core;

public class LayeredFileSystemException : Exception
{
    public LayeredFileSystemException(string message) : base(message) { }
    public LayeredFileSystemException(string message, Exception inner) : base(message, inner) { }
}

public class DuplicatePathException : LayeredFileSystemException
{
    public string Path { get; }
    public string ExistingPath { get; }
    
    public DuplicatePathException(string path, string existingPath) 
        : base($"Path '{path}' conflicts with existing path '{existingPath}'")
    {
        Path = path;
        ExistingPath = existingPath;
    }
}

public class LayerNotFoundException : LayeredFileSystemException
{
    public string Hash { get; }
    
    public LayerNotFoundException(string hash) 
        : base($"Layer with hash '{hash}' not found in cache")
    {
        Hash = hash;
    }
}

