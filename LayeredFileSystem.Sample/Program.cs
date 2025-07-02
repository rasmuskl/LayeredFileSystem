Console.WriteLine("LayeredFileSystem Sample Application");
Console.WriteLine("=====================================");

var tempDir = Path.GetTempPath();
var workingDir = Path.Combine(tempDir, "layered-fs-sample", Guid.NewGuid().ToString());
var cacheDir = Path.Combine(tempDir, "layered-fs-cache");

try
{
    // Ensure directories exist
    Directory.CreateDirectory(workingDir);
    Directory.CreateDirectory(cacheDir);
    
    // Clear cache to ensure consistent demo behavior
    if (Directory.Exists(cacheDir))
    {
        Directory.Delete(cacheDir, recursive: true);
        Directory.CreateDirectory(cacheDir);
        Console.WriteLine("Cleared cache for consistent demo");
    }

    Console.WriteLine($"Working Directory: {workingDir}");
    Console.WriteLine($"Cache Directory: {cacheDir}");
    Console.WriteLine();

    var fileSystem = new LayeredFileSystem.Core.LayeredFileSystem();
    using var session = await fileSystem.CreateSessionAsync(workingDir, cacheDir);

    Console.WriteLine("Created layered file system session");
    Console.WriteLine();

    // Step 1: Initial setup
    Console.WriteLine("Step 1: Creating initial layer with config file");
    using (var layer1 = await session.BeginLayerAsync("setup-base-v1"))
    {
        Console.WriteLine($"Layer from cache: {layer1.IsFromCache}");
        
        if (!layer1.IsFromCache)
        {
            // Create initial files
            var configPath = Path.Combine(session.WorkingDirectory, "config.json");
            await File.WriteAllTextAsync(configPath, @"{
  ""name"": ""LayeredFileSystem Sample"",
  ""version"": ""1.0.0""
}");
            
            var srcDir = Path.Combine(session.WorkingDirectory, "src");
            Directory.CreateDirectory(srcDir);
            
            Console.WriteLine("Created config.json and src directory");
        }
        
        var layerInfo = await layer1.CommitAsync();
        Console.WriteLine($"Layer committed: {layerInfo.Hash[..8]}...");
        Console.WriteLine($"Files added: {layerInfo.Statistics.FilesAdded}");
        Console.WriteLine($"Directories added: {layerInfo.Statistics.DirectoriesAdded}");
    }

    Console.WriteLine();

    // Step 2: Add source files
    Console.WriteLine("Step 2: Adding source files and modifying config");
    using (var layer2 = await session.BeginLayerAsync("add-source-v1"))
    {
        Console.WriteLine($"Layer from cache: {layer2.IsFromCache}");
        
        if (!layer2.IsFromCache)
        {
            // Add source file
            var mainPath = Path.Combine(session.WorkingDirectory, "src", "Program.cs");
            await File.WriteAllTextAsync(mainPath, @"using System;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello from layered file system!"");
        }
    }
}");

            // Modify config
            var configPath = Path.Combine(session.WorkingDirectory, "config.json");
            await File.WriteAllTextAsync(configPath, @"{
  ""name"": ""LayeredFileSystem Sample"",
  ""version"": ""1.1.0"",
  ""description"": ""Updated with source files""
}");
            
            Console.WriteLine("Added Program.cs and updated config.json");
        }
        
        var layerInfo = await layer2.CommitAsync();
        Console.WriteLine($"Layer committed: {layerInfo.Hash[..8]}...");
        Console.WriteLine($"Files added: {layerInfo.Statistics.FilesAdded}");
        Console.WriteLine($"Files modified: {layerInfo.Statistics.FilesModified}");
    }

    Console.WriteLine();
    Console.WriteLine("Applied Layers Summary (Session 1):");
    Console.WriteLine("===================================");
    
    for (int i = 0; i < session.AppliedLayers.Count; i++)
    {
        var layer = session.AppliedLayers[i];
        Console.WriteLine($"Layer {i + 1}: {layer.Hash[..8]}... ({layer.SizeBytes} bytes)");
        Console.WriteLine($"  Created: {layer.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Stats: +{layer.Statistics.FilesAdded}F +{layer.Statistics.DirectoriesAdded}D ~{layer.Statistics.FilesModified}F -{layer.Statistics.FilesDeleted}F");
    }

    Console.WriteLine();
    Console.WriteLine("Final working directory contents (Session 1):");
    Console.WriteLine("=============================================");
    
    PrintDirectoryContents(session.WorkingDirectory, "");
    
    Console.WriteLine();

    // Step 3: Demonstrate cache reuse with a new session
    Console.WriteLine("Step 3: Testing cache hit with new session");
    var newWorkingDir = Path.Combine(tempDir, "layered-fs-sample", Guid.NewGuid().ToString());
    Directory.CreateDirectory(newWorkingDir);
    
    Console.WriteLine($"New Working Directory: {newWorkingDir}");
    
    using var newSession = await fileSystem.CreateSessionAsync(newWorkingDir, cacheDir);
    
    // Recreate the same layers - should hit cache
    using (var cachedLayer1 = await newSession.BeginLayerAsync("setup-base-v1"))
    {
        Console.WriteLine($"Layer 1 from cache: {cachedLayer1.IsFromCache}");
        await cachedLayer1.CommitAsync();
    }
    
    using (var cachedLayer2 = await newSession.BeginLayerAsync("add-source-v1"))
    {
        Console.WriteLine($"Layer 2 from cache: {cachedLayer2.IsFromCache}");
        await cachedLayer2.CommitAsync();
    }
    
    Console.WriteLine();
    Console.WriteLine("Final working directory contents (Session 2 - from cache):");
    Console.WriteLine("=========================================================");
    
    PrintDirectoryContents(newSession.WorkingDirectory, "");
    
    // Clean up second working directory
    Directory.Delete(newWorkingDir, recursive: true);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
}
finally
{
    // Clean up
    if (Directory.Exists(workingDir))
    {
        Directory.Delete(workingDir, recursive: true);
        Console.WriteLine();
        Console.WriteLine("Cleaned up working directory");
    }
}

static void PrintDirectoryContents(string path, string indent)
{
    try
    {
        foreach (var file in Directory.GetFiles(path))
        {
            var fileName = Path.GetFileName(file);
            var size = new FileInfo(file).Length;
            Console.WriteLine($"{indent}[FILE] {fileName} ({size} bytes)");
        }

        foreach (var directory in Directory.GetDirectories(path))
        {
            var dirName = Path.GetFileName(directory);
            Console.WriteLine($"{indent}[DIR]  {dirName}/");
            PrintDirectoryContents(directory, indent + "  ");
        }
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"{indent}[ERROR] Access denied");
    }
}
