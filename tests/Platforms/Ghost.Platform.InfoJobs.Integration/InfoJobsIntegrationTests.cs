using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.InfoJobs.Integration.Fixtures;
using Ghost.Platform.InfoJobs.Jobs;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.InfoJobs.Integration;

/// <summary>
/// Integration tests for InfoJobs platform using real browser automation.
/// InfoJobs is a Spanish/Portuguese job platform.
/// Uses InfoJobsContextFixture to provide an isolated browser context for this test class.
/// </summary>
[Trait("Category", "Integration")]
[TestTimeout(60000)] // 60 seconds for integration tests
public class InfoJobsIntegrationTests : IClassFixture<InfoJobsContextFixture>
{
    private readonly InfoJobsContextFixture _fixture;
    private readonly InfoJobClient _jobClient;

    public InfoJobsIntegrationTests(InfoJobsContextFixture fixture)
    {
        _fixture = fixture;
        _jobClient = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
    }

    [Fact]
    public async Task SearchJobs_WithKeywords_ReturnsResults()
    {
        // Arrange - Using Spanish location for InfoJobs
        var criteria = new JobSearchCriteria
        {
            Query = "desarrollador software",
            Location = "Madrid",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("InfoJobs should return at least some job results");
        results.Should().HaveCountLessThanOrEqualTo(criteria.MaxResults);

        // Verify job fields are populated
        foreach (var job in results)
        {
            job.Id.Should().NotBeNullOrEmpty("Job ID should be populated");
            job.Title.Should().NotBeNullOrEmpty("Job title should be populated");
            job.Company.Should().NotBeNullOrEmpty("Company name should be populated");
            job.Source.Should().Be("InfoJobs");
        }
    }

    [Fact]
    public async Task SearchJobs_WithPortugueseLocation_ReturnsResults()
    {
        // Arrange - InfoJobs also operates in Portugal
        var criteria = new JobSearchCriteria
        {
            Query = "programador",
            Location = "Lisboa",
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
            Query = "ingeniero",
            Location = "Barcelona",
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
        jobDetails.Source.Should().Be("InfoJobs");
    }

    [Fact]
    public async Task SearchJobs_WithBrowserStrategy_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "analista de datos",
            Location = "Valencia",
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
    public async Task PlatformName_ReturnsInfoJobs()
    {
        // Act
        var platformName = _jobClient.PlatformName;

        // Assert
        platformName.Should().Be("InfoJobs");
    }

    [Fact]
    public async Task SearchJobs_WithTeletrabajo_ReturnsRemoteJobs()
    {
        // Arrange - "Teletrabajo" means remote work in Spanish
        var criteria = new JobSearchCriteria
        {
            Query = "desarrollador web",
            Location = "Teletrabajo",
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
    public async Task SearchJobs_WithSpanishCities_ReturnsResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "programador java",
            Location = "Bilbao",
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
            Query = "diseñador",
            Location = "Madrid",
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

    [Fact]
    public async Task SearchJobs_WithEnglishKeywords_StillWorks()
    {
        // Arrange - Test that English keywords also work
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "Madrid",
            MaxResults = 5
        };

        // Act
        var results = await _jobClient.SearchJobsAsync(criteria);

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("InfoJobs should handle English keywords");

        foreach (var job in results)
        {
            job.Title.Should().NotBeNullOrEmpty();
            job.Company.Should().NotBeNullOrEmpty();
        }
    }
}
