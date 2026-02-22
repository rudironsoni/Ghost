using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// HTTP-based smoke tests for multi-platform aggregation.
/// Tests the Ghost API endpoints for aggregating jobs across multiple platforms.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Flow", "MultiPlatform")]
public class MultiPlatformHttpSmokeTests : IClassFixture<GhostWebApiFixture>
{
    private readonly GhostWebApiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public MultiPlatformHttpSmokeTests(GhostWebApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact(Skip = "Depends on external platform integrations that are disabled in tests")]
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
        _output.WriteLine($"Searching all platforms via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results!.Count} jobs across all platforms");

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

        _output.WriteLine("\n=== Platform Distribution ===");
        _output.WriteLine($"Platforms contributing data: {platformDistribution.Count}");
        foreach (var platform in platformDistribution)
        {
            _output.WriteLine($"  - {platform.Platform}: {platform.Count} jobs");
        }

        // Assert that we have results from multiple platforms
        platformDistribution.Should().HaveCountGreaterThan(1,
            "aggregation should return results from multiple platforms");
    }

    [Fact(Skip = "Depends on external platform integrations that are disabled in tests")]
    public async Task SearchJobs_WithMultiplePlatforms_Returns_Deduplicated_Results()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 30
        };

        // Act
        _output.WriteLine($"Searching for deduplication via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results!.Count} jobs");

        // Validate no duplicates
        results.AssertNoDuplicateJobs();

        // Check for potential duplicates by title and company
        var potentialDuplicates = results
            .GroupBy(j => new { j.Title, j.Company })
            .Where(g => g.Count() > 1)
            .Select(g => new { Title = g.Key.Title, Company = g.Key.Company, Count = g.Count() })
            .ToList();

        _output.WriteLine($"\n=== Potential Duplicates by Title/Company ===");
        _output.WriteLine($"Found {potentialDuplicates.Count} potential duplicates");
        foreach (var dup in potentialDuplicates)
        {
            _output.WriteLine($"  - '{dup.Title}' at {dup.Company}: {dup.Count} occurrences");
        }

        // Note: Some duplicates by title/company are expected across platforms
        // but job IDs should be unique (validated by AssertNoDuplicateJobs)
    }

    [Fact(Skip = "Depends on external platform integrations that are disabled in tests")]
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
        _output.WriteLine($"Searching all platforms via API for: {searchRequest.query} in {searchRequest.location}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results!.Count} jobs");

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

        _output.WriteLine("\n=== Top 5 Locations ===");
        foreach (var loc in locationDistribution)
        {
            _output.WriteLine($"  - {loc.Location}: {loc.Count} jobs");
        }

        // Check for remote jobs
        var remoteJobs = results.Where(j =>
            !string.IsNullOrEmpty(j.Location) &&
            (j.Location.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
             j.Location.Contains("Anywhere", StringComparison.OrdinalIgnoreCase))).ToList();

        _output.WriteLine($"\n=== Remote Jobs ===");
        _output.WriteLine($"Found {remoteJobs.Count} remote jobs");
    }

    [Fact(Skip = "Depends on external platform integrations that are disabled in tests")]
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
            _output);

        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        // Get one job from each platform
        var jobsByPlatform = searchResults!
            .Where(j => j.Source != null)
            .GroupBy(j => j.Source!)
            .Select(g => g.First())
            .ToList();

        _output.WriteLine($"\n=== Testing Job Details from {jobsByPlatform.Count} Platforms ===");

        // Act & Assert - Get details for each job
        foreach (JobListing? job in jobsByPlatform)
        {
            _output.WriteLine($"\nTesting {job.Source} job: {job.Id}");

            JobListing? jobDetails = await _fixture.GetAsync<JobListing>($"/api/jobs/{job.Id}", _output);

            jobDetails.Should().NotBeNull("job details should not be null");
            jobDetails!.Id.Should().Be(job.Id, "job ID should match the requested ID");
            jobDetails.Source.Should().Be(job.Source, "source should match");

            // Validate required fields
            jobDetails.AssertRequiredFields();
            jobDetails.AssertValidPlatformId(job.Source!);
            jobDetails.AssertUrlReachable();

            _output.WriteLine($"  ✓ Valid job details for {job.Source}");
        }
    }
}
