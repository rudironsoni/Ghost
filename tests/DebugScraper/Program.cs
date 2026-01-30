using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Platform.Glassdoor.Internal;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Platform.Indeed.Internal;
using Ghost.Platform.Indeed;
using Ghost.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Tests.DebugScraper;

public sealed class NoOpProxyProvider : IProxyProvider
{
    public Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
    {
        return Task.FromResult<ProxyInfo?>(null);
    }
}

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var http = new HttpClient(handler);

        Console.WriteLine("--- Starting Debug Scraper ---");

        // Ensure logs directory exists
        System.IO.Directory.CreateDirectory("logs");

        // 1. Google Jobs
        Console.WriteLine("\n[Google Jobs] Searching...");
        try
        {
            var googleClient = new GoogleJobsApiClient(http, loggerFactory.CreateLogger<GoogleJobsApiClient>());
            var googleResults = await googleClient.SearchAsync("Software Engineer", "New York");
            Console.WriteLine($"Google Jobs Found: {googleResults.Count}");
            if (googleResults.Count > 0)
            {
                Console.WriteLine($"  - Sample: {googleResults[0].Title} at {googleResults[0].Company}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Google Jobs Error: {ex.Message}");
        }

        Console.WriteLine("\n[Glassdoor] Searching...");
        try
        {
            var glassdoorClient = new GlassdoorApiClient(http);
            var glassdoorResults = await glassdoorClient.SearchAsync("Software Engineer", "New York");
            Console.WriteLine($"Glassdoor Response Length: {glassdoorResults?.Length ?? 0}");
            if (!string.IsNullOrEmpty(glassdoorResults) && !glassdoorResults.Contains("Server error"))
            {
                Console.WriteLine("  - Glassdoor returning valid data");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Glassdoor Error: {ex.Message}");
        }

        Console.WriteLine("\n[Indeed] Searching...");
        try
        {
            var indeedOptions = new IndeedOptions { ApiKey = "161092c2017b5bbab13edb12461a62d5a833871e7cad6d9d475304573de67ac8", Country = CountryCode.US }; 
            var indeedClient = new IndeedApiClient(new NoOpProxyProvider(), indeedOptions, loggerFactory.CreateLogger<IndeedApiClient>());
            int count = 0;
            var sampleJobs = new List<string>();
            await foreach (var job in indeedClient.SearchAsync("Software Engineer", "New York"))
            {
                count++;
                if (count <= 2)
                {
                    var root = job.GetProperty("data").GetProperty("jobSearch").GetProperty("results")[0].GetProperty("job");
                    var title = root.GetProperty("title").GetString();
                    if (!string.IsNullOrEmpty(title))
                    {
                        sampleJobs.Add(title);
                    }
                }
            }
            Console.WriteLine($"Indeed Jobs Found: {count}");
            foreach (var job in sampleJobs)
            {
                Console.WriteLine($"  - Sample: {job}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Indeed Error: {ex.Message}");
        }

        Console.WriteLine("\n--- Done ---");
    }
}