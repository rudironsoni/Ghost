using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Sdk.Spider.Engine;

namespace Ghost.Platform.LinkedIn.Tests.Migration;

/// <summary>
/// Spider for crawling LinkedIn job postings using Ghost.Sdk.Spider framework.
/// This demonstrates the migration from platform-specific scraping to the unified Spider SDK.
/// </summary>
public class LinkedInSpider : Spider
{
    private readonly EntityParser _parser;
    private readonly List<LinkedInJobEntity> _extractedJobs = new();

    public override string Name => "LinkedInJobSpider";

    public override SpiderOptions Options { get; } = new()
    {
        AllowedDomains = new List<string> { "linkedin.com" },
        ExcludePatterns = new List<string> { @".*/admin/.*", @".*/logout.*" },
        MaxDepth = 2,
        MaxConcurrency = 5,
        RequestDelay = TimeSpan.FromSeconds(1)
    };

    /// <summary>
    /// Gets the jobs extracted by this spider
    /// </summary>
    public IReadOnlyList<LinkedInJobEntity> ExtractedJobs => _extractedJobs.AsReadOnly();

    public LinkedInSpider()
    {
        _parser = new EntityParser();
    }

    public override IEnumerable<string> GetStartUrls()
    {
        // Example start URLs for LinkedIn job search
        return new[]
        {
            "https://www.linkedin.com/jobs/view/software-engineer-new-grad-at-stripe-4294691514",
            "https://www.linkedin.com/jobs/search/?keywords=software%20engineer&location=United%20States"
        };
    }

    public override async Task ProcessResponseAsync(
        Response response,
        Ghost.Sdk.Spider.Engine.ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Only process successful HTML responses
        if (!response.IsSuccess || response.Content?.ContentType != ContentType.JavaScript)
        {
            return;
        }

        var extractionContext = new ExtractionContext
        {
            Content = response.Content.Content ?? string.Empty,
            SourceUrl = response.FinalUrl ?? "unknown",
            Timestamp = response.RespondedAt.DateTime
        };

        // Extract job entities from the page
        var jobs = _parser.Parse<LinkedInJobEntity>(extractionContext);

        foreach (var job in jobs)
        {
            if (job.Validate())
            {
                _extractedJobs.Add(job);

                // Log extraction (in real scenario, you'd use proper logging)
                Console.WriteLine($"Extracted job: {job.Title} at {job.Company}");
            }
        }

        // Optionally extract and follow pagination links
        // In a real implementation, you would parse pagination links and add them to the queue
        await Task.CompletedTask;
    }

    public override Task OnStartAsync(Ghost.Sdk.Spider.Engine.ExecutionContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Starting {Name} spider...");
        _extractedJobs.Clear();
        return Task.CompletedTask;
    }

    public override Task OnCompleteAsync(
        Ghost.Sdk.Spider.Engine.ExecutionContext context,
        SpiderResult result,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Completed {Name} spider. Extracted {_extractedJobs.Count} jobs.");
        return Task.CompletedTask;
    }

    public override Task OnErrorAsync(
        Exception exception,
        Ghost.Sdk.Spider.Engine.ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Error in {Name} spider: {exception.Message}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if a URL should be followed based on LinkedIn-specific rules
    /// </summary>
    public override bool ShouldFollowUrl(string url, Ghost.Sdk.Spider.Engine.ExecutionContext context)
    {
        if (!base.ShouldFollowUrl(url, context))
            return false;

        // Only follow job listing and search pages
        if (!url.Contains("/jobs/view/") && !url.Contains("/jobs/search/"))
            return false;

        return true;
    }


}
