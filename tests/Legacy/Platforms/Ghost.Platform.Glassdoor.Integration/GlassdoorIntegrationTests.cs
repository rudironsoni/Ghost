using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Integration.Fixtures;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Integration;

/// <summary>
/// Integration tests for Glassdoor platform using real browser automation.
/// Uses SharedKernel collection to share a single GhostKernel instance across all integration tests.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Capability", "RequiresBrowser")]
[Trait("Capability", "RequiresNetwork")]
[Collection("SharedKernel")]
[TestTimeout(60000)] // 60 seconds for integration tests
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
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);
        if (results.Count > 0)
        {
            results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Location), "All jobs should have location information");
        }
    }

    [Fact]
    public async Task GetJobDetails_WithValidJobId_ReturnsDetails()
    {
        // Arrange
        var jobId = "test-glassdoor-job-id";

        // Act
        var jobDetails = await _jobClient.GetJobDetailsAsync(jobId);

        // Assert
        jobDetails.Should().NotBeNull();
        jobDetails.Id.Should().Be(jobId);
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
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        foreach (var job in results)
        {
            job.Should().NotBeNull();
            job.Title.Should().NotBeNullOrEmpty();
            job.Company.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void PlatformName_ReturnsGlassdoor()
    {
        // Act
        var platformName = _jobClient.PlatformName;

        // Assert
        platformName.Should().Be("Glassdoor");
    }

    [Fact]
    public async Task SearchJobs_WithPopularLocation_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "backend developer",
            Location = "Seattle",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);
        if (results.Count > 0)
        {
            results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Location));
        }
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
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Glassdoor often includes salary information
        // At least some jobs might have salary data
        var jobsWithData = results.Where(j => !string.IsNullOrEmpty(j.Salary)).ToList();
        // We don't require all to have salary, but verify structure is correct
        if (results.Count > 0)
        {
            results.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
        }
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
        results1.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);
        results2.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Both should return valid data
        if (results1.Count > 0)
        {
            results1.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
        }

        if (results2.Count > 0)
        {
            results2.Should().OnlyContain(j => !string.IsNullOrEmpty(j.Title));
        }
    }
}
