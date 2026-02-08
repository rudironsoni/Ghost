using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ghost.Platform.Google.Jobs;
using Ghost.Platform.Google.Jobs.Internal;

namespace Ghost.Test;

public class TestGoogleJobs
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Testing Google Jobs Direct HTML Scraping");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var logger = NullLogger<GoogleJobsApiClient>.Instance;
        var options = new GoogleJobsOptions();

        var client = new GoogleJobsApiClient(httpClient, options, logger);

        Console.WriteLine("Fetching jobs for 'software engineer' in 'San Francisco'...");
        Console.WriteLine();

        try
        {
            var jobs = await client.SearchAsync("software engineer", "San Francisco");

            Console.WriteLine($"Found {jobs.Count} jobs:");
            Console.WriteLine();

            foreach (var job in jobs.Take(10))
            {
                Console.WriteLine($"Title: {job.Title}");
                Console.WriteLine($"Company: {job.Company}");
                Console.WriteLine($"Location: {job.Location}");
                Console.WriteLine($"Source: {job.Source}");
                if (!string.IsNullOrEmpty(job.Url))
                {
                    Console.WriteLine($"URL: {job.Url}");
                }
                if (!string.IsNullOrEmpty(job.Description))
                {
                    var desc = job.Description.Length > 100
                        ? job.Description.Substring(0, 100) + "..."
                        : job.Description;
                    Console.WriteLine($"Description: {desc}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("SUCCESS: Direct HTML scraping works!");
            Console.WriteLine($"Total jobs found: {jobs.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}
