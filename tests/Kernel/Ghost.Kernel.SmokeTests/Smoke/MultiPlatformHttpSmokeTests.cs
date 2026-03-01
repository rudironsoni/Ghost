using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// HTTP-based smoke tests for multi-platform aggregation.
/// Tests the Ghost API endpoints for aggregating jobs across multiple platforms.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Flow", "MultiPlatform")]
public class MultiPlatformHttpSmokeTests : ReliabilityTestBase, IClassFixture<GhostWebApiFixture>
{
    private readonly GhostWebApiFixture _fixture;

    public MultiPlatformHttpSmokeTests(GhostWebApiFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
    }

    [ConditionalFact("MultiPlatform")]
    public async Task SearchJobs_AcrossAllPlatforms_Returns_Aggregated_Results()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 50
            // No platform specified - should search all enabled platforms
        };

        // Act
        Output.WriteLine($"Searching all platforms via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results!.Count} jobs across all platforms");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate freshness for all jobs
        foreach (JobListing job in results)
        {
            job.AssertFreshData(TimeSpan.FromDays(90));
        }

        // Analyze platform distribution
        var platformDistribution = results
            .Where(j => j.Source != null)
            .GroupBy(j => j.Source!)
            .Select(g => new { Platform = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        Output.WriteLine("\n=== Platform Distribution ===");
        Output.WriteLine($"Platforms contributing data: {platformDistribution.Count}");
        foreach (var platform in platformDistribution)
        {
            Output.WriteLine($"  - {platform.Platform}: {platform.Count} jobs");
        }

        // Assert that we have results from multiple platforms
        platformDistribution.Should().HaveCountGreaterThan(1,
            "aggregation should return results from multiple platforms");
    }

    [ConditionalFact("MultiPlatform")]
    public async Task SearchJobs_WithMultiplePlatforms_Returns_Deduplicated_Results()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 30
        };

        // Act
        Output.WriteLine($"Searching for deduplication via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results!.Count} jobs");

        // Validate no duplicates
        results.AssertNoDuplicateJobs();

        // Check for potential duplicates by title and company
        var potentialDuplicates = results
            .GroupBy(j => new { j.Title, j.Company })
            .Where(g => g.Count() > 1)
            .Select(g => new { Title = g.Key.Title, Company = g.Key.Company, Count = g.Count() })
            .ToList();

        Output.WriteLine($"\n=== Potential Duplicates by Title/Company ===");
        Output.WriteLine($"Found {potentialDuplicates.Count} potential duplicates");
        foreach (var dup in potentialDuplicates)
        {
            Output.WriteLine($"  - '{dup.Title}' at {dup.Company}: {dup.Count} occurrences");
        }

        // Note: Some duplicates by title/company are expected across platforms
        // but job IDs should be unique (validated by AssertNoDuplicateJobs)
    }

    [ConditionalFact("MultiPlatform")]
    public async Task SearchJobs_WithLocation_AcrossPlatforms_Returns_LocationAware_Results()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            location = "Remote",
            maxResults = 30
        };

        // Act
        Output.WriteLine($"Searching all platforms via API for: {searchRequest.query} in {searchRequest.location}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results!.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that jobs have location information
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        jobsWithLocation.Should().NotBeEmpty("at least some jobs should have location information");

        // Analyze location distribution
        var locationDistribution = results
            .Where(j => !string.IsNullOrEmpty(j.Location))
            .GroupBy(j => j.Location!)
            .Select(g => new { Location = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToList();

        Output.WriteLine("\n=== Top 5 Locations ===");
        foreach (var loc in locationDistribution)
        {
            Output.WriteLine($"  - {loc.Location}: {loc.Count} jobs");
        }

        // Check for remote jobs
        var remoteJobs = results.Where(j =>
            !string.IsNullOrEmpty(j.Location) &&
            (j.Location.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
             j.Location.Contains("Anywhere", StringComparison.OrdinalIgnoreCase))).ToList();

        Output.WriteLine($"\n=== Remote Jobs ===");
        Output.WriteLine($"Found {remoteJobs.Count} remote jobs");
    }

    [ConditionalFact("MultiPlatform")]
    public async Task GetJobDetails_FromMultiplePlatforms_Returns_Valid_Data()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 10
        };

        // First, search for jobs to get valid IDs from different platforms
        List<JobListing>? searchResults = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        // Get one job from each platform
        var jobsByPlatform = searchResults!
            .Where(j => j.Source != null)
            .GroupBy(j => j.Source!)
            .Select(g => g.First())
            .ToList();

        Output.WriteLine($"\n=== Testing Job Details from {jobsByPlatform.Count} Platforms ===");

        // Act & Assert - Get details for each job
        foreach (JobListing? job in jobsByPlatform)
        {
            Output.WriteLine($"\nTesting {job.Source} job: {job.Id}");

            JobListing? jobDetails = await _fixture.GetAsync<JobListing>($"/api/jobs/{job.Id}", Output);

            jobDetails.Should().NotBeNull("job details should not be null");
            jobDetails!.Id.Should().Be(job.Id, "job ID should match the requested ID");
            jobDetails.Source.Should().Be(job.Source, "source should match");

            // Validate required fields
            jobDetails.AssertRequiredFields();
            jobDetails.AssertValidPlatformId(job.Source!);
            jobDetails.AssertUrlReachable();

            Output.WriteLine($"  ✓ Valid job details for {job.Source}");
        }
    }
}
