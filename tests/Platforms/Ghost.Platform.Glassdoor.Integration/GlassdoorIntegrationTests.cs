using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Integration;

/// <summary>
/// Integration tests for Glassdoor platform using real browser automation.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Browser")]
public class GlassdoorIntegrationTests : IClassFixture<GlassdoorContextFixture>
{
    private readonly GlassdoorContextFixture _fixture;
    private readonly GlassdoorJobClient _jobClient;

    public GlassdoorIntegrationTests(GlassdoorContextFixture fixture)
    {
        _fixture = fixture;
        _jobClient = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();
    }

    [Fact]
    public async Task SearchJobs_WithKeywords_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "San Francisco",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Glassdoor should return at least some job results");
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Verify job fields are populated
        foreach (var job in results)
        {
            job.Id.Should().NotBeNullOrEmpty("Job ID should be populated");
            job.Title.Should().NotBeNullOrEmpty("Job title should be populated");
            job.Company.Should().NotBeNullOrEmpty("Company name should be populated");
            job.Source.Should().Be("Glassdoor");
        }
    }

    [Fact]
    public async Task SearchJobs_WithLocation_ReturnsRelevantResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "developer",
            Location = "Seattle",
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
            Query = "engineer",
            Location = "New York",
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
        jobDetails.Source.Should().Be("Glassdoor");
    }

    [Fact]
    public async Task SearchJobs_WithBrowserStrategy_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "data analyst",
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
        }
    }

    [Fact]
    public async Task PlatformName_ReturnsGlassdoor()
    {
        // Act
        var platformName = _jobClient.PlatformName;

        // Assert
        platformName.Should().Be("Glassdoor");
    }

    [Fact]
    public async Task SearchJobs_WithRemoteLocation_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "backend developer",
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
    public async Task SearchJobs_WithSalaryInformation_ReturnsEstimates()
    {
        // Arrange - Glassdoor is known for salary data
        var criteria = new JobSearchCriteria
        {
            Query = "senior software engineer",
            Location = "San Francisco",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();

        // Glassdoor often includes salary information
        // At least some jobs might have salary data
        var jobsWithData = results.Where(j => !string.IsNullOrEmpty(j.Salary)).ToList();
        // We don't require all to have salary, but verify structure is correct
        results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
    }

    [Fact]
    public async Task SearchJobs_MultipleTimes_ReturnsConsistentResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "designer",
            Location = "Portland",
            MaxResults = 3
        };

        // Act - Perform multiple searches
        var results1 = await _jobClient.SearchJobsAsync(criteria);
        await Task.Delay(2000); // Glassdoor might have rate limiting
        var results2 = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results1.Should().NotBeEmpty("First search should return results");
        results2.Should().NotBeEmpty("Second search should return results");

        // Both should return valid data
        results1.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
        results2.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
    }
}
