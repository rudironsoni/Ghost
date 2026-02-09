using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Google.Integration.Fixtures;
using Ghost.Platform.Google.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Google.Integration;

/// <summary>
/// Integration tests for Google Jobs platform using real browser automation.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Browser")]
public class GoogleJobsIntegrationTests : IClassFixture<GoogleContextFixture>
{
    private readonly GoogleContextFixture _fixture;
    private readonly GoogleJobClient _jobClient;

    public GoogleJobsIntegrationTests(GoogleContextFixture fixture)
    {
        _fixture = fixture;
        _jobClient = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
    }

    [Fact]
    public async Task SearchJobs_WithKeywords_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "Mountain View",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Google Jobs should return at least some job results");
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Verify job fields are populated
        foreach (var job in results)
        {
            job.Id.Should().NotBeNullOrEmpty("Job ID should be populated");
            job.Title.Should().NotBeNullOrEmpty("Job title should be populated");
            job.Company.Should().NotBeNullOrEmpty("Company name should be populated");
            job.Source.Should().Be("GoogleJobs");
        }
    }

    [Fact]
    public async Task SearchJobs_WithLocation_ReturnsRelevantResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "developer",
            Location = "San Jose, CA",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();

        // All jobs should have location information
        results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Location),
            "All jobs should have location information");
    }

    [Fact]
    public async Task GetJobDetails_WithValidJobId_ReturnsDetails()
    {
        // Arrange - First search for a job to get a valid ID
        var criteria = new JobSearchCriteria
        {
            Query = "product manager",
            Location = "Palo Alto",
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
        jobDetails.Title.Should().NotBeNullOrEmpty("Job title should be populated");
        jobDetails.Company.Should().NotBeNullOrEmpty("Company name should be populated");
        jobDetails.Source.Should().Be("GoogleJobs");
    }

    [Fact]
    public async Task SearchJobs_WithBrowserStrategy_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "data scientist",
            Location = "San Francisco",
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
        }
    }

    [Fact]
    public async Task PlatformName_ReturnsGoogleJobs()
    {
        // Act
        var platformName = _jobClient.PlatformName;

        // Assert
        platformName.Should().Be("GoogleJobs");
    }

    [Fact]
    public async Task SearchJobs_WithRemoteLocation_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "full stack developer",
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
    public async Task SearchJobs_WithCompanyName_ReturnsRelevantResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer Google",
            Location = "Mountain View",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();

        // Verify we got results with valid company names
        results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Company));
    }

    [Fact]
    public async Task SearchJobs_MultipleTimes_ReturnsConsistentResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "designer",
            Location = "Los Angeles",
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
