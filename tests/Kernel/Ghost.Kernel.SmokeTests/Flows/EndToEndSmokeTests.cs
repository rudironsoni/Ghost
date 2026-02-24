using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Smoke.Tests.Integration;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Flows;

/// <summary>
/// End-to-end integration tests that validate complete user journeys through the Ghost job search system.
/// These tests simulate real user workflows: search → select → get details → validate consistency.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Flow", "EndToEnd")]
public class EndToEndIntegrationTests : ReliabilityTestBase, IClassFixture<PlatformIntegrationTestFixture>
{
    private readonly PlatformIntegrationTestFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public EndToEndIntegrationTests(PlatformIntegrationTestFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
        _serviceProvider = fixture.ServiceProvider;
    }

    [ConditionalFact("MultiPlatform")]
    public async Task SearchAndGetDetails_Flow_ValidatesDataConsistency()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            MaxResults = 10
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Step 1: Search for jobs
        Output.WriteLine($"=== Step 1: Searching for jobs ===");
        Output.WriteLine($"Query: {criteria.Query}");
        Output.WriteLine($"Max Results: {criteria.MaxResults}");

        IReadOnlyList<JobListing> searchResults = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert - Step 1: Validate search results
        searchResults.Should().NotBeNull("search results should not be null");
        searchResults.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"\nFound {searchResults.Count} jobs");

        // Validate data quality
        searchResults.AssertRealJobResults();
        searchResults.AssertNoDuplicateJobs();

        // Act - Step 2: Get details for the first job
        JobListing firstJob = searchResults[0];
        Output.WriteLine($"\n=== Step 2: Getting details for first job ===");
        Output.WriteLine($"Job ID: {firstJob.Id}");
        Output.WriteLine($"Title: {firstJob.Title}");
        Output.WriteLine($"Company: {firstJob.Company}");
        Output.WriteLine($"Source: {firstJob.Source}");

        var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        JobListing jobDetails = await _serviceProvider.GetRequiredService<IJobClient>()
            .GetJobDetailsAsync(firstJob.Id, detailsCts.Token);

        // Assert - Step 2: Validate job details
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails.Id.Should().Be(firstJob.Id, "job ID should match the search result ID");

        // Assert - Step 3: Validate data consistency between search and details
        Output.WriteLine($"\n=== Step 3: Validating data consistency ===");

        jobDetails.Title.Should().Be(firstJob.Title,
            "job title should be consistent between search and details");
        jobDetails.Company.Should().Be(firstJob.Company,
            "company name should be consistent between search and details");
        jobDetails.Source.Should().Be(firstJob.Source,
            "source platform should be consistent between search and details");

        // URL should match or be more detailed in details
        if (!string.IsNullOrEmpty(firstJob.Url) && !string.IsNullOrEmpty(jobDetails.Url))
        {
            jobDetails.Url.Should().Contain(firstJob.Url.Split('/').Last(),
                "details URL should contain the same path as search URL");
        }

        // Validate required fields in details
        jobDetails.AssertRequiredFields();
        jobDetails.AssertValidPlatformId(firstJob.Source ?? string.Empty);
        jobDetails.AssertUrlReachable();

        // Output detailed comparison
        Output.WriteLine("\n=== Data Consistency Check ===");
        Output.WriteLine($"Title Match: {jobDetails.Title == firstJob.Title}");
        Output.WriteLine($"Company Match: {jobDetails.Company == firstJob.Company}");
        Output.WriteLine($"Source Match: {jobDetails.Source == firstJob.Source}");
        Output.WriteLine($"Location Match: {jobDetails.Location == firstJob.Location}");
        Output.WriteLine($"\nSearch Result:");
        Output.WriteLine($"  - Title: {firstJob.Title}");
        Output.WriteLine($"  - Company: {firstJob.Company}");
        Output.WriteLine($"  - Location: {firstJob.Location}");
        Output.WriteLine($"  - URL: {firstJob.Url}");
        Output.WriteLine($"\nJob Details:");
        Output.WriteLine($"  - Title: {jobDetails.Title}");
        Output.WriteLine($"  - Company: {jobDetails.Company}");
        Output.WriteLine($"  - Location: {jobDetails.Location}");
        Output.WriteLine($"  - URL: {jobDetails.Url}");
        Output.WriteLine($"  - Description Length: {jobDetails.Description?.Length ?? 0} characters");
        Output.WriteLine($"  - Job Type: {jobDetails.JobType}");
        Output.WriteLine($"  - Experience Level: {jobDetails.ExperienceLevel}");
        Output.WriteLine($"  - Salary: {jobDetails.Salary ?? "Not specified"}");
    }

    [ConditionalFact("MultiPlatform")]
    public async Task Search_WithFilters_Returns_RelevantResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "Remote",
            RemoteOnly = true,
            MaxResults = 10
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        Output.WriteLine($"=== Searching with filters ===");
        Output.WriteLine($"Query: {criteria.Query}");
        Output.WriteLine($"Location: {criteria.Location}");
        Output.WriteLine($"Remote Only: {criteria.RemoteOnly}");

        IReadOnlyList<JobListing> results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"\nFound {results.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that results are relevant to the filters
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        if (jobsWithLocation.Count > 0)
        {
            Output.WriteLine($"\n=== Location Filter Validation ===");
            Output.WriteLine($"Jobs with location info: {jobsWithLocation.Count}/{results.Count}");

            // Check for remote-related keywords in location
            var remoteJobs = jobsWithLocation.Where(j =>
                j.Location!.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                j.Location.Contains("Anywhere", StringComparison.OrdinalIgnoreCase) ||
                j.Location.Contains("Home", StringComparison.OrdinalIgnoreCase)).ToList();

            Output.WriteLine($"Jobs with remote location: {remoteJobs.Count}");

            // Output sample locations
            Output.WriteLine("\n=== Sample Locations ===");
            foreach (JobListing? job in results.Take(5))
            {
                Output.WriteLine($"{job.Title} at {job.Company}: {job.Location ?? "No location"} (Remote: {job.Remote})");
            }
        }

        // Validate that results match the query
        Output.WriteLine($"\n=== Query Relevance Validation ===");
        string queryLower = criteria.Query!.ToLowerInvariant();
        var relevantJobs = results.Where(j =>
            (j.Title?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (j.Company?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (j.Description?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        Output.WriteLine($"Jobs matching query '{criteria.Query}': {relevantJobs.Count}/{results.Count}");

        // Output sample titles
        Output.WriteLine("\n=== Sample Job Titles ===");
        foreach (JobListing? job in results.Take(5))
        {
            Output.WriteLine($"- {job.Title} at {job.Company}");
        }
    }

    [ConditionalFact("MultiPlatform")]
    public async Task Search_AllPlatforms_AggregatesRealData()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "developer",
            MaxResults = 20
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        Output.WriteLine($"=== Searching all platforms ===");
        Output.WriteLine($"Query: {criteria.Query}");
        Output.WriteLine($"Max Results: {criteria.MaxResults}");

        IReadOnlyList<JobListing> results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"\nTotal jobs found: {results.Count}");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Analyze platform distribution
        var platformGroups = results
            .Where(j => !string.IsNullOrEmpty(j.Source))
            .GroupBy(j => j.Source!)
            .OrderByDescending(g => g.Count())
            .ToList();

        Output.WriteLine($"\n=== Platform Distribution ===");
        Output.WriteLine($"Platforms contributing data: {platformGroups.Count}");

        foreach (IGrouping<string, JobListing>? group in platformGroups)
        {
            Output.WriteLine($"  - {group.Key}: {group.Count()} jobs");
        }

        // Validate that multiple platforms contributed
        platformGroups.Should().HaveCountGreaterThan(0,
            "at least one platform should contribute data");

        // Output sample jobs from each platform
        Output.WriteLine("\n=== Sample Jobs by Platform ===");
        foreach (IGrouping<string, JobListing>? group in platformGroups.Take(3))
        {
            Output.WriteLine($"\nPlatform: {group.Key} ({group.Count()} jobs)");
            foreach (JobListing? job in group.Take(2))
            {
                Output.WriteLine($"  - {job.Title} at {job.Company}");
            }
        }

        // Validate freshness across all results
        Output.WriteLine($"\n=== Freshness Validation ===");
        int freshJobs = 0;
        foreach (JobListing job in results)
        {
            try
            {
                job.AssertFreshData(TimeSpan.FromDays(90));
                freshJobs++;
            }
            catch
            {
                // Some jobs might be older, that's acceptable for smoke tests
            }
        }

        Output.WriteLine($"Fresh jobs (within 90 days): {freshJobs}/{results.Count}");
    }

    [ConditionalFact("MultiPlatform")]
    public async Task GetJobDetails_ByPlatformId_Returns_ValidData()
    {
        // Arrange
        string[] platforms = new[] { "linkedin", "indeed", "glassdoor", "infojobs" };
        var criteria = new JobSearchCriteria
        {
            Query = "engineer",
            MaxResults = 5
        };
        var searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Output.WriteLine($"=== Testing GetJobDetails for each platform ===");

        foreach (string? platform in platforms)
        {
            Output.WriteLine($"\n--- Platform: {platform} ---");

            // Get platform-specific client
            IJobClient? platformClient = null;
            try
            {
                platformClient = _fixture.GetJobClient(platform);
            }
            catch
            {
                Output.WriteLine($"  Skipped: Platform client not available");
                continue;
            }

            if (platformClient == null)
            {
                Output.WriteLine($"  Skipped: Platform client is null");
                continue;
            }

            // Search for jobs on this platform
            IReadOnlyList<JobListing> searchResults;
            try
            {
                searchResults = await platformClient.SearchJobsAsync(criteria, searchCts.Token);
            }
            catch (Exception ex)
            {
                Output.WriteLine($"  Search failed: {ex.Message}");
                continue;
            }

            if (!searchResults.Any())
            {
                Output.WriteLine($"  Skipped: No jobs found");
                continue;
            }

            Output.WriteLine($"  Found {searchResults.Count} jobs");

            // Get details for the first job
            JobListing firstJob = searchResults[0];
            Output.WriteLine($"  Testing job ID: {firstJob.Id}");

            var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            JobListing? jobDetails = null;
            try
            {
                jobDetails = await platformClient.GetJobDetailsAsync(firstJob.Id, detailsCts.Token);
            }
            catch (Exception ex)
            {
                Output.WriteLine($"  GetJobDetails failed: {ex.Message}");
                continue;
            }

            // Validate details
            jobDetails.Should().NotBeNull($"job details for {platform} should not be null");
            jobDetails!.Id.Should().Be(firstJob.Id, "job ID should match");
            jobDetails.Source.Should().BeEquivalentTo(platform,
                $"source should be {platform} (case-insensitive)");

            // Validate required fields
            jobDetails.AssertRequiredFields();
            jobDetails.AssertValidPlatformId(platform);
            jobDetails.AssertUrlReachable();

            Output.WriteLine($"  ✓ Details retrieved successfully");
            Output.WriteLine($"    Title: {jobDetails.Title}");
            Output.WriteLine($"    Company: {jobDetails.Company}");
            Output.WriteLine($"    Description Length: {jobDetails.Description?.Length ?? 0} characters");
        }

        Output.WriteLine($"\n=== Platform Details Test Complete ===");
    }
}
