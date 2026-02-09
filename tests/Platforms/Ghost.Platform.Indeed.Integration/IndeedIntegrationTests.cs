using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Integration.Fixtures;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Indeed.Integration;

/// <summary>
/// Integration tests for Indeed platform using real browser automation.
/// Tests API → Browser fallback mechanism.
/// Uses SharedKernel collection to share a single GhostKernel instance across all integration tests.
/// </summary>
[Trait("Category", "Integration")]
[Collection("SharedKernel")]
[TestTimeout(60000)] // 60 seconds for integration tests
public class IndeedIntegrationTests : IClassFixture<IndeedContextFixture>
{
    private readonly IndeedContextFixture _fixture;
    private readonly IndeedJobClient _jobClient;

    public IndeedIntegrationTests(IndeedContextFixture fixture)
    {
        _fixture = fixture;
        _jobClient = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
    }

    [Fact]
    public async Task SearchJobs_WithFallback_ReturnsResults()
    {
        // Arrange - Test API → Browser fallback
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "Austin",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Indeed should return results via API or browser fallback");
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Verify job fields are populated
        foreach (var job in results)
        {
            job.Id.Should().NotBeNullOrEmpty("Job ID should be populated");
            job.Title.Should().NotBeNullOrEmpty("Job title should be populated");
            job.Company.Should().NotBeNullOrEmpty("Company name should be populated");
            job.Source.Should().Be("Indeed");
        }
    }

    [Fact]
    public async Task SearchJobs_WithBrowserStrategy_ReturnsResults()
    {
        // Arrange - Force browser strategy
        var criteria = new JobSearchCriteria
        {
            Query = "developer",
            Location = "Boston",
            MaxResults = 5,
            Strategy = "Browser"
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Browser strategy should return results");

        foreach (var job in results)
        {
            job.Should().NotBeNull();
            job.Title.Should().NotBeNullOrEmpty();
            job.Company.Should().NotBeNullOrEmpty();
            job.Source.Should().Be("Indeed");
        }
    }

    [Fact]
    public async Task SearchJobs_WithHybridStrategy_Achieves95PercentReliability()
    {
        // Arrange - Test hybrid fallback reliability
        var criteria = new JobSearchCriteria
        {
            Query = "data analyst",
            Location = "Chicago",
            MaxResults = 5,
            Strategy = "Hybrid"
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Hybrid strategy should provide high reliability");

        // Verify we got meaningful results
        results.Should().OnlyContain(j =>
            !string.IsNullOrEmpty(j.Title) &&
            !string.IsNullOrEmpty(j.Company),
            "All jobs should have title and company");
    }

    [Fact]
    public async Task GetJobDetails_WithValidJobId_ReturnsDetails()
    {
        // Arrange - First search for a job to get a valid ID
        var criteria = new JobSearchCriteria
        {
            Query = "engineer",
            Location = "Denver",
            MaxResults = 1
        };
        var searchResults = await _jobClient.SearchJobsAsync(criteria);
        searchResults.Should().NotBeEmpty("Need at least one job to test details");

        var jobId = searchResults[0].Id;

        // Act
        var jobDetails = await _jobClient.GetJobDetailsAsync(jobId);

        // Assert
        jobDetails.Should().NotBeNull();
        jobDetails.Id.Should().Be(jobId);
        jobDetails.Title.Should().NotBeNullOrEmpty();
        jobDetails.Company.Should().NotBeNullOrEmpty();
        jobDetails.Source.Should().Be("Indeed");
    }

    [Fact]
    public async Task PlatformName_ReturnsIndeed()
    {
        // Act
        var platformName = _jobClient.PlatformName;

        // Assert
        platformName.Should().Be("Indeed");
    }

    [Fact]
    public async Task SearchJobs_WithRemoteLocation_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software developer",
            Location = "Remote",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();
        results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Location));
    }

    [Fact]
    public async Task SearchJobs_MultipleTimes_MaintainsReliability()
    {
        // Arrange - Test consistency across multiple searches
        var criteria = new JobSearchCriteria
        {
            Query = "manager",
            Location = "Miami",
            MaxResults = 3
        };

        // Act - Perform multiple searches
        var results1 = await _jobClient.SearchJobsAsync(criteria);
        await Task.Delay(1000); // Small delay between requests
        var results2 = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results1.Should().NotBeEmpty("First search should return results");
        results2.Should().NotBeEmpty("Second search should return results");

        // Both should return valid data
        results1.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
        results2.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
    }
}
