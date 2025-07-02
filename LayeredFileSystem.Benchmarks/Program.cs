using BenchmarkDotNet.Running;
using LayeredFileSystem.Benchmarks;

namespace LayeredFileSystem.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].ToLowerInvariant() == "cache")
        {
            BenchmarkRunner.Run<CacheBenchmarks>();
        }
        else if (args.Length > 0 && args[0].ToLowerInvariant() == "overhead")
        {
            BenchmarkRunner.Run<OverheadBenchmarks>();
        }
        else
        {
            Console.WriteLine("LayeredFileSystem Benchmarks");
            Console.WriteLine("============================");
            Console.WriteLine();
            Console.WriteLine("Available benchmark suites:");
            Console.WriteLine("  cache    - Cache hit vs miss performance");
            Console.WriteLine("  overhead - System overhead measurements");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project LayeredFileSystem.Benchmarks cache");
            Console.WriteLine("  dotnet run --project LayeredFileSystem.Benchmarks overhead");
            Console.WriteLine();
            Console.WriteLine("Running all benchmarks...");
            Console.WriteLine();
            
            BenchmarkRunner.Run<CacheBenchmarks>();
            BenchmarkRunner.Run<OverheadBenchmarks>();
        }
    }
}
