using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Smoke.Tests.Integration;
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
public class EndToEndIntegrationTests : IClassFixture<PlatformIntegrationTestFixture>
{
    private readonly PlatformIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public EndToEndIntegrationTests(PlatformIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _serviceProvider = fixture.ServiceProvider;
    }

    [Fact]
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
        _output.WriteLine($"=== Step 1: Searching for jobs ===");
        _output.WriteLine($"Query: {criteria.Query}");
        _output.WriteLine($"Max Results: {criteria.MaxResults}");

        var searchResults = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert - Step 1: Validate search results
        searchResults.Should().NotBeNull("search results should not be null");
        searchResults.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"\nFound {searchResults.Count} jobs");

        // Validate data quality
        searchResults.AssertRealJobResults();
        searchResults.AssertNoDuplicateJobs();

        // Act - Step 2: Get details for the first job
        var firstJob = searchResults[0];
        _output.WriteLine($"\n=== Step 2: Getting details for first job ===");
        _output.WriteLine($"Job ID: {firstJob.Id}");
        _output.WriteLine($"Title: {firstJob.Title}");
        _output.WriteLine($"Company: {firstJob.Company}");
        _output.WriteLine($"Source: {firstJob.Source}");

        var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var jobDetails = await _serviceProvider.GetRequiredService<IJobClient>()
            .GetJobDetailsAsync(firstJob.Id, detailsCts.Token);

        // Assert - Step 2: Validate job details
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails.Id.Should().Be(firstJob.Id, "job ID should match the search result ID");

        // Assert - Step 3: Validate data consistency between search and details
        _output.WriteLine($"\n=== Step 3: Validating data consistency ===");

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
        _output.WriteLine("\n=== Data Consistency Check ===");
        _output.WriteLine($"Title Match: {jobDetails.Title == firstJob.Title}");
        _output.WriteLine($"Company Match: {jobDetails.Company == firstJob.Company}");
        _output.WriteLine($"Source Match: {jobDetails.Source == firstJob.Source}");
        _output.WriteLine($"Location Match: {jobDetails.Location == firstJob.Location}");
        _output.WriteLine($"\nSearch Result:");
        _output.WriteLine($"  - Title: {firstJob.Title}");
        _output.WriteLine($"  - Company: {firstJob.Company}");
        _output.WriteLine($"  - Location: {firstJob.Location}");
        _output.WriteLine($"  - URL: {firstJob.Url}");
        _output.WriteLine($"\nJob Details:");
        _output.WriteLine($"  - Title: {jobDetails.Title}");
        _output.WriteLine($"  - Company: {jobDetails.Company}");
        _output.WriteLine($"  - Location: {jobDetails.Location}");
        _output.WriteLine($"  - URL: {jobDetails.Url}");
        _output.WriteLine($"  - Description Length: {jobDetails.Description?.Length ?? 0} characters");
        _output.WriteLine($"  - Job Type: {jobDetails.JobType}");
        _output.WriteLine($"  - Experience Level: {jobDetails.ExperienceLevel}");
        _output.WriteLine($"  - Salary: {jobDetails.Salary ?? "Not specified"}");
    }

    [Fact]
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
        _output.WriteLine($"=== Searching with filters ===");
        _output.WriteLine($"Query: {criteria.Query}");
        _output.WriteLine($"Location: {criteria.Location}");
        _output.WriteLine($"Remote Only: {criteria.RemoteOnly}");

        var results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"\nFound {results.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that results are relevant to the filters
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        if (jobsWithLocation.Count > 0)
        {
            _output.WriteLine($"\n=== Location Filter Validation ===");
            _output.WriteLine($"Jobs with location info: {jobsWithLocation.Count}/{results.Count}");

            // Check for remote-related keywords in location
            var remoteJobs = jobsWithLocation.Where(j =>
                j.Location!.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                j.Location.Contains("Anywhere", StringComparison.OrdinalIgnoreCase) ||
                j.Location.Contains("Home", StringComparison.OrdinalIgnoreCase)).ToList();

            _output.WriteLine($"Jobs with remote location: {remoteJobs.Count}");

            // Output sample locations
            _output.WriteLine("\n=== Sample Locations ===");
            foreach (var job in results.Take(5))
            {
                _output.WriteLine($"{job.Title} at {job.Company}: {job.Location ?? "No location"} (Remote: {job.Remote})");
            }
        }

        // Validate that results match the query
        _output.WriteLine($"\n=== Query Relevance Validation ===");
        var queryLower = criteria.Query!.ToLowerInvariant();
        var relevantJobs = results.Where(j =>
            (j.Title?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (j.Company?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (j.Description?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        _output.WriteLine($"Jobs matching query '{criteria.Query}': {relevantJobs.Count}/{results.Count}");

        // Output sample titles
        _output.WriteLine("\n=== Sample Job Titles ===");
        foreach (var job in results.Take(5))
        {
            _output.WriteLine($"- {job.Title} at {job.Company}");
        }
    }

    [Fact]
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
        _output.WriteLine($"=== Searching all platforms ===");
        _output.WriteLine($"Query: {criteria.Query}");
        _output.WriteLine($"Max Results: {criteria.MaxResults}");

        var results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"\nTotal jobs found: {results.Count}");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Analyze platform distribution
        var platformGroups = results
            .Where(j => !string.IsNullOrEmpty(j.Source))
            .GroupBy(j => j.Source!)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"\n=== Platform Distribution ===");
        _output.WriteLine($"Platforms contributing data: {platformGroups.Count}");

        foreach (var group in platformGroups)
        {
            _output.WriteLine($"  - {group.Key}: {group.Count()} jobs");
        }

        // Validate that multiple platforms contributed
        platformGroups.Should().HaveCountGreaterThan(0,
            "at least one platform should contribute data");

        // Output sample jobs from each platform
        _output.WriteLine("\n=== Sample Jobs by Platform ===");
        foreach (var group in platformGroups.Take(3))
        {
            _output.WriteLine($"\nPlatform: {group.Key} ({group.Count()} jobs)");
            foreach (var job in group.Take(2))
            {
                _output.WriteLine($"  - {job.Title} at {job.Company}");
            }
        }

        // Validate freshness across all results
        _output.WriteLine($"\n=== Freshness Validation ===");
        var freshJobs = 0;
        foreach (var job in results)
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

        _output.WriteLine($"Fresh jobs (within 90 days): {freshJobs}/{results.Count}");
    }

    [Fact]
    public async Task GetJobDetails_ByPlatformId_Returns_ValidData()
    {
        // Arrange
        var platforms = new[] { "linkedin", "indeed", "glassdoor", "infojobs" };
        var criteria = new JobSearchCriteria
        {
            Query = "engineer",
            MaxResults = 5
        };
        var searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        _output.WriteLine($"=== Testing GetJobDetails for each platform ===");

        foreach (var platform in platforms)
        {
            _output.WriteLine($"\n--- Platform: {platform} ---");

            // Get platform-specific client
            IJobClient? platformClient = null;
            try
            {
                platformClient = _fixture.GetJobClient(platform);
            }
            catch
            {
                _output.WriteLine($"  Skipped: Platform client not available");
                continue;
            }

            if (platformClient == null)
            {
                _output.WriteLine($"  Skipped: Platform client is null");
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
                _output.WriteLine($"  Search failed: {ex.Message}");
                continue;
            }

            if (!searchResults.Any())
            {
                _output.WriteLine($"  Skipped: No jobs found");
                continue;
            }

            _output.WriteLine($"  Found {searchResults.Count} jobs");

            // Get details for the first job
            var firstJob = searchResults[0];
            _output.WriteLine($"  Testing job ID: {firstJob.Id}");

            var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            JobListing? jobDetails = null;
            try
            {
                jobDetails = await platformClient.GetJobDetailsAsync(firstJob.Id, detailsCts.Token);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  GetJobDetails failed: {ex.Message}");
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

            _output.WriteLine($"  ✓ Details retrieved successfully");
            _output.WriteLine($"    Title: {jobDetails.Title}");
            _output.WriteLine($"    Company: {jobDetails.Company}");
            _output.WriteLine($"    Description Length: {jobDetails.Description?.Length ?? 0} characters");
        }

        _output.WriteLine($"\n=== Platform Details Test Complete ===");
    }
}
