using System.Globalization;
using System.Text.Json;
using Ghost.Contracts.Jobs;
using Ghost.LinkedInScraperHarness.Configuration;
using Ghost.Plugin.LinkedIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.LinkedInScraperHarness;

/// <summary>
/// Hosted service that runs the LinkedIn scraper harness.
/// </summary>
public sealed class ScraperHarnessRunner : IHostedService
{
    private readonly ILogger<ScraperHarnessRunner> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ScraperHarnessOptions _options;

    public ScraperHarnessRunner(
        ILogger<ScraperHarnessRunner> logger,
        IServiceProvider serviceProvider,
        IOptions<ScraperHarnessOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? new ScraperHarnessOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LinkedIn Scraper Harness starting...");

        try
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();

            LinkedInJobClient jobClient = scope.ServiceProvider.GetRequiredService<LinkedInJobClient>();

            if (_options.InteractiveMode)
            {
                await RunInteractiveModeAsync(jobClient, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RunSingleSearchAsync(jobClient, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("LinkedIn Scraper Harness completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running scraper harness");
            throw;
        }

        // Stop the application after completion
        IHostApplicationLifetime lifetime = _serviceProvider.GetRequiredService<IHostApplicationLifetime>();
        lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task RunSingleSearchAsync(LinkedInJobClient jobClient, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching for jobs with keywords: {Keywords}, location: {Location}",
            _options.SearchKeywords, _options.Location);

        JobSearchCriteria criteria = new()
        {
            Query = _options.SearchKeywords,
            Location = _options.Location,
            MaxResults = _options.MaxResults
        };

        IReadOnlyList<JobListing> jobs = await jobClient.SearchJobsAsync(criteria, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Found {Count} jobs", jobs.Count);

        if (_options.FetchDetails && jobs.Count > 0)
        {
            jobs = await FetchJobDetailsAsync(jobClient, jobs, cancellationToken).ConfigureAwait(false);
        }

        await OutputResultsAsync(jobs, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<JobListing>> FetchJobDetailsAsync(
        LinkedInJobClient jobClient,
        IReadOnlyList<JobListing> jobs,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching detailed information for {Count} jobs...", jobs.Count);

        List<JobListing> detailedJobs = [];

        foreach (JobListing job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                JobListing detailedJob = await jobClient.GetJobDetailsAsync(job.Id, cancellationToken)
                    .ConfigureAwait(false);
                detailedJobs.Add(detailedJob);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch details for job {JobId}: {Title}", job.Id, job.Title);
                detailedJobs.Add(job);
            }
        }

        return detailedJobs;
    }

    private async Task OutputResultsAsync(IReadOnlyList<JobListing> jobs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        switch (_options.OutputFormat.ToLowerInvariant())
        {
            case "json":
                OutputJson(jobs);
                break;
            case "csv":
                OutputCsv(jobs);
                break;
            case "table":
            default:
                OutputTable(jobs);
                break;
        }
    }

    private static void OutputJson(IReadOnlyList<JobListing> jobs)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(jobs, options);
        Console.WriteLine(json);
    }

    private static void OutputCsv(IReadOnlyList<JobListing> jobs)
    {
        Console.WriteLine("Id,Title,Company,Location,JobType,ExperienceLevel,PostedAt,Url,Description");

        foreach (JobListing job in jobs)
        {
            string description = job.Description?.Replace("\"", "\"\"") ?? string.Empty;
            Console.WriteLine(
                $"\"{job.Id}\",\"{job.Title}\",\"{job.Company}\",\"{job.Location}\",\"{job.JobType}\",\"{job.ExperienceLevel}\",\"{job.PostedAt:yyyy-MM-dd}\",\"{job.Url}\",\"{description}\"");
        }
    }

    private static void OutputTable(IReadOnlyList<JobListing> jobs)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 120));
        Console.WriteLine($"Found {jobs.Count} jobs");
        Console.WriteLine(new string('=', 120));

        foreach (JobListing job in jobs)
        {
            Console.WriteLine();
            Console.WriteLine($"  ID:           {job.Id}");
            Console.WriteLine($"  Title:        {job.Title}");
            Console.WriteLine($"  Company:      {job.Company}");
            Console.WriteLine($"  Location:     {job.Location}");
            Console.WriteLine($"  Job Type:     {job.JobType}");
            Console.WriteLine($"  Experience:   {job.ExperienceLevel}");
            Console.WriteLine($"  Posted:       {job.PostedAt:yyyy-MM-dd}");
            Console.WriteLine($"  Easy Apply:   {job.IsEasyApply}");
            Console.WriteLine($"  URL:          {job.Url}");

            if (!string.IsNullOrWhiteSpace(job.Description))
            {
                string description = job.Description.Length > 300
                    ? job.Description[..300] + "..."
                    : job.Description;
                Console.WriteLine($"  Description:  {description}");
            }

            if (job.Salary != null)
            {
                Console.WriteLine($"  Salary:       {job.Salary}");
            }

            Console.WriteLine(new string('-', 120));
        }
    }

    private async Task RunInteractiveModeAsync(LinkedInJobClient jobClient, CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     LinkedIn Scraper Harness - Interactive Mode            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.Write("Enter search keywords (or 'quit' to exit): ");
            string? keywords = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(keywords) ||
                keywords.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            Console.Write("Enter location (press Enter for 'Remote'): ");
            string? location = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(location))
            {
                location = "Remote";
            }

            Console.Write("Enter max results (default 10): ");
            string? maxResultsInput = Console.ReadLine();

            int maxResults = int.TryParse(maxResultsInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMax)
                ? parsedMax
                : 10;

            JobSearchCriteria criteria = new()
            {
                Query = keywords,
                Location = location,
                MaxResults = maxResults
            };

            _logger.LogInformation("Searching for jobs with keywords: {Keywords}, location: {Location}",
                keywords, location);

            IReadOnlyList<JobListing> jobs = await jobClient.SearchJobsAsync(criteria, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Found {Count} jobs", jobs.Count);

            await OutputResultsAsync(jobs, cancellationToken).ConfigureAwait(false);
        }
    }
}
