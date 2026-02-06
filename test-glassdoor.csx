#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Microsoft.Extensions.Options, 9.0.0"
#r "/tmp/ghost-build/bin/Ghost.Contracts.Jobs/Debug/net9.0/Ghost.Contracts.Jobs.dll"
#r "/tmp/ghost-build/bin/Ghost.Platform.Glassdoor/Debug/net10.0/Ghost.Platform.Glassdoor.dll"

using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor;
using Ghost.Platform.Glassdoor.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

Console.WriteLine("=== Glassdoor Real Scraping Test ===\n");

// Setup DI container
var services = new ServiceCollection();
services.AddLogging(builder => {
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Glassdoor options
services.Configure<GlassdoorOptions>(opts => {
    opts.Enabled = true;
    opts.ApiKey = null; // No API key needed for GraphQL scraping
});

// Register internal clients
services.AddSingleton<GlassdoorApiClient>();
services.AddSingleton<GlassdoorBrowserClient>();
services.AddSingleton<GlassdoorJobClient>();

var provider = services.BuildServiceProvider();

// Get the client
var client = provider.GetRequiredService<GlassdoorJobClient>();

// Search for jobs
var criteria = new JobSearchCriteria
{
    Query = "software engineer",
    Location = "San Francisco, CA",
    MaxResults = 5
};

Console.WriteLine($"Searching for: {criteria.Query} in {criteria.Location}");
Console.WriteLine($"Max results: {criteria.MaxResults}\n");

try
{
    var jobs = await client.SearchJobsAsync(criteria);
    
    Console.WriteLine($"\n=== RESULTS: Found {jobs.Count} jobs ===\n");
    
    int count = 0;
    foreach (var job in jobs)
    {
        count++;
        if (count > 5) break; // Limit to 5 for display
        
        Console.WriteLine($"Job #{count}:");
        Console.WriteLine($"  Title: {job.Title ?? "N/A"}");
        Console.WriteLine($"  Company: {job.Company ?? "N/A"}");
        Console.WriteLine($"  Location: {job.Location ?? "N/A"}");
        Console.WriteLine($"  URL: {job.Url ?? "N/A"}");
        Console.WriteLine($"  ID: {job.Id ?? "N/A"}");
        Console.WriteLine($"  Salary: {job.Salary ?? "N/A"}");
        Console.WriteLine();
    }
    
    // Summary
    Console.WriteLine("=== VERIFICATION ===");
    Console.WriteLine($"✓ Total jobs scraped: {jobs.Count}");
    Console.WriteLine($"✓ Using migrated GlassdoorJobClient: YES");
    Console.WriteLine($"✓ Real data from Glassdoor: {(jobs.Count > 0 ? "YES" : "NO")}");
    Console.WriteLine($"✓ GlassdoorJobEntity extraction: {(jobs.Count > 0 && jobs[0].Title != null ? "WORKING" : "FAILED")}");
}
catch (Exception ex)
{
    Console.WriteLine($"\n=== ERROR ===");
    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
}
