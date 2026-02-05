using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Ghost.Platform.Google.Jobs.Internal;

namespace Ghost.Test.GoogleJobsDirect;

/// <summary>
/// Standalone test to verify Google Jobs DIRECT HTML SCRAPING works
/// NO SERPAPI, NO EXTERNAL APIs - just direct HTTP requests to Google
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Google Jobs DIRECT HTML SCRAPING Test                ║");
        Console.WriteLine("║  NO SerpAPI | NO External APIs | Pure HTML Parsing    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(options => options.FormatterName = "simple");
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<GoogleJobsApiClient>();
        
        // Create HttpClient with proper configuration
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = true
        };
        
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var options = new Ghost.Platform.Google.Jobs.GoogleJobsOptions();
        var client = new GoogleJobsApiClient(httpClient, options, logger);

        Console.WriteLine("🔍 Searching for: 'software engineer' in 'San Francisco'");
        Console.WriteLine("📡 Making direct HTTP request to Google Jobs...");
        Console.WriteLine();

        try
        {
            var jobs = await client.SearchAsync("software engineer", "San Francisco");

            Console.WriteLine();
            Console.WriteLine($"✅ SUCCESS! Found {jobs.Count} jobs via DIRECT HTML SCRAPING");
            Console.WriteLine();

            if (jobs.Count == 0)
            {
                Console.WriteLine("⚠️  No jobs returned - this might be due to:");
                Console.WriteLine("   - Google consent pages");
                Console.WriteLine("   - CAPTCHA challenges");
                Console.WriteLine("   - Rate limiting");
                Console.WriteLine("   - HTML structure changes");
                Console.WriteLine();
                Console.WriteLine("💡 Check the logs/ directory for saved HTML responses");
                return;
            }

            Console.WriteLine("📋 First 5 jobs:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            int count = 0;
            foreach (var job in jobs.Take(5))
            {
                count++;
                Console.WriteLine();
                Console.WriteLine($"Job #{count}:");
                Console.WriteLine($"  📌 Title:    {job.Title ?? "N/A"}");
                Console.WriteLine($"  🏢 Company:  {job.Company ?? "N/A"}");
                Console.WriteLine($"  📍 Location: {job.Location ?? "N/A"}");
                Console.WriteLine($"  🌐 Source:   {job.Source ?? "N/A"}");
                
                if (!string.IsNullOrEmpty(job.Description))
                {
                    var desc = job.Description.Length > 100 
                        ? job.Description.Substring(0, 100) + "..." 
                        : job.Description;
                    Console.WriteLine($"  📄 Summary:  {desc}");
                }
                
                if (!string.IsNullOrEmpty(job.Url))
                {
                    Console.WriteLine($"  🔗 URL:      {job.Url}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine($"✅ VERIFICATION COMPLETE");
            Console.WriteLine($"   Total jobs scraped: {jobs.Count}");
            Console.WriteLine($"   Method: Direct HTTP + HTML Parsing");
            Console.WriteLine($"   API Key Required: NO");
            Console.WriteLine($"   SerpAPI Used: NO");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
            Console.WriteLine("💡 This might be due to:");
            Console.WriteLine("   - Network connectivity issues");
            Console.WriteLine("   - Google blocking the request");
            Console.WriteLine("   - HTML structure changes");
            Console.WriteLine();
            Environment.Exit(1);
        }
    }
}
