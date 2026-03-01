using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;

namespace Ghost.Scraper.Benchmarks;

/// <summary>
/// Entry point for Ghost Scraper benchmarks.
/// Run with: dotnet run --project benchmarks/Ghost.Scraper.Benchmarks/Ghost.Scraper.Benchmarks.csproj --configuration Release
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Ghost Scraper Benchmarks");
        Console.WriteLine("========================\n");

        ManualConfig config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

        BenchmarkRunner.Run(typeof(Program).Assembly, config, args);
    }
}
