using System;
using System.Threading.Tasks;
using Ghost.Platform.Google.Jobs;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.TestGoogleJobs;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Google Jobs Scraping Test ===");
        Console.WriteLine("Testing the migrated GoogleJobClient with Ghost.Sdk.Spider");
        Console.WriteLine();

        // Create logger factory
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<GoogleJobClient>();
        var apiLogger = loggerFactory.CreateLogger<GoogleJobsApiClient>();

        // Create HttpClient
        using var httpClient = new System.Net.Http.HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        // Create options
        var options = Options.Create(new GoogleJobsOptions
        {
            Strategy = JobSearchStrategy.HttpOnly,
            MaxResults = 5
        });

        // Create API client
        var apiClient = new GoogleJobsApiClient(httpClient, options.Value, apiLogger);

        // Create GoogleJobClient (HttpOnly - no browser needed)
        var jobClient = new GoogleJobClient(apiClient, logger, options);

        try
        {
            Console.WriteLine("Searching for 'software engineer' jobs...");
            Console.WriteLine();

            var criteria = new JobSearchCriteria
            {
                Query = "software engineer",
                Location = "San Francisco, CA",
                MaxResults = 5
            };

            var results = await jobClient.SearchJobsAsync(criteria);

            Console.WriteLine($"\n=== RESULTS: Found {results.Count} jobs ===\n");

            for (int i = 0; i < results.Count; i++)
            {
                var job = results[i];
                Console.WriteLine($"--- Job #{i + 1} ---");
                Console.WriteLine($"Title:       {job.Title}");
                Console.WriteLine($"Company:     {job.CompanyName}");
                Console.WriteLine($"Location:    {job.Location}");
                Console.WriteLine($"Posted:      {job.PostedAt}");
                Console.WriteLine($"Description: {(job.Description?.Length > 100 ? job.Description.Substring(0, 100) + "..." : job.Description)}");
                Console.WriteLine($"URL:         {job.JobUrl}");
                Console.WriteLine();
            }

            if (results.Count == 0)
            {
                Console.WriteLine("⚠️  NO JOBS FOUND");
                Console.WriteLine("This could be due to:");
                Console.WriteLine("  - Google blocking the request (CAPTCHA/consent page)");
                Console.WriteLine("  - HTML structure changed");
                Console.WriteLine("  - Network connectivity issues");
                Console.WriteLine();
                Console.WriteLine("Check the logs directory for google_jobs_search.html to see the raw HTML response");
            }
            else
            {
                Console.WriteLine("✅ SUCCESS: GoogleJobsEntity extraction worked!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
