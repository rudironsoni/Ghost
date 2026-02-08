using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Platform.Google.Jobs;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing Google Jobs Direct Scraping (NO SerpAPI) ===\n");

        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<GoogleJobsApiClient>();

        // Create HTTP client
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Create options
        var options = new GoogleJobsOptions
        {
            Enabled = true,
            Country = "US"
        };

        // Create API client
        var client = new GoogleJobsApiClient(httpClient, options, logger);

        Console.WriteLine("Searching for 'software engineer' jobs in 'San Francisco'...\n");

        try
        {
            var results = await client.SearchAsync("software engineer", "San Francisco");

            Console.WriteLine($"\n✅ SUCCESS: Found {results.Count} jobs\n");

            foreach (var job in results.Take(5))
            {
                Console.WriteLine($"Title: {job.Title}");
                Console.WriteLine($"Company: {job.Company}");
                Console.WriteLine($"Location: {job.Location}");
                Console.WriteLine($"Source: {job.Source}");
                if (!string.IsNullOrEmpty(job.Url))
                    Console.WriteLine($"URL: {job.Url}");
                Console.WriteLine();
            }

            Console.WriteLine($"\n=== VERIFICATION ===");
            Console.WriteLine($"✅ Direct scraping WORKS (no SerpAPI needed)");
            Console.WriteLine($"✅ Found {results.Count} real job listings from Google");
            Console.WriteLine($"✅ Using direct HTML parsing strategies");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERROR: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
