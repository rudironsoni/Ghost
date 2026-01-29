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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Google Jobs Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        // 2. Glassdoor
        Console.WriteLine("\n[Glassdoor] Searching...");
        try
        {
            var glassdoorClient = new GlassdoorApiClient(http);
            var glassdoorResults = await glassdoorClient.SearchAsync("Software Engineer", "New York");
            Console.WriteLine($"Glassdoor Response Length: {glassdoorResults?.Length ?? 0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Glassdoor Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        // 3. Indeed
        Console.WriteLine("\n[Indeed] Searching...");
        try
        {
            var indeedOptions = new IndeedOptions { ApiKey = "161092c2017b5bbab13edb12461a62d5a833871e7cad6d9d475304573de67ac8", Country = CountryCode.US }; 
            var indeedClient = new IndeedApiClient(new NoOpProxyProvider(), indeedOptions, loggerFactory.CreateLogger<IndeedApiClient>());
            int count = 0;
            await foreach (var job in indeedClient.SearchAsync("Software Engineer", "New York"))
            {
                count++;
            }
            Console.WriteLine($"Indeed Jobs Found: {count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Indeed Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("\n--- Done ---");
    }
}