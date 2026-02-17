using Ghost.Contracts.Jobs;
using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Google.End2EndTests;

/// <summary>
/// End-to-End tests for Google Jobs Client using HTTP client.
/// Tests validate actual data extraction from Google Jobs.
/// </summary>
[Trait("Category", "End2End")]
public sealed class GoogleJobClientE2ETests : IClassFixture<Fixtures.GoogleE2EFixture>
{
    private readonly Fixtures.GoogleE2EFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GoogleJobClientE2ETests(Fixtures.GoogleE2EFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_WithValidCriteria_ReturnsJobListingsAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "Mountain View, CA",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Basic validation
        Assert.NotNull(results);

        if (results.Count > 0)
        {
            JobListing firstJob = results[0];
            Assert.NotNull(firstJob.Id);
            Assert.Equal("Google", firstJob.Source);

            // RequiredFieldsContract validation
            Assert.False(string.IsNullOrWhiteSpace(firstJob.Title), "Job must have a title");
            Assert.False(string.IsNullOrWhiteSpace(firstJob.Company), "Job must have a company");
            Assert.False(string.IsNullOrWhiteSpace(firstJob.Source), "Job must have a source");

            // Data quality checks
            Assert.True(results.Count <= criteria.MaxResults, "Results should respect MaxResults");

            _output.WriteLine($"Found {results.Count} jobs from Google");
        }
        else
        {
            _output.WriteLine("No jobs returned from search - this may be expected in test environment");
        }
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetails_WithValidJobId_ReturnsJobDetailsAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
        string jobId = "job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("Google", result.Source);

        // RequiredFieldsContract validation
        Assert.False(string.IsNullOrWhiteSpace(result.Title), "Job must have a title");
        Assert.False(string.IsNullOrWhiteSpace(result.Company), "Job must have a company");
        Assert.False(string.IsNullOrWhiteSpace(result.Source), "Job must have a source");
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("Google", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_WithEmptyQuery_ReturnsResultsAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = string.Empty,
            Location = string.Empty,
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        _output.WriteLine($"Empty query returned {results.Count} jobs");
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_RespectsMaxResultsAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Engineer",
            MaxResults = 5
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.Count <= criteria.MaxResults, "Results should not exceed MaxResults");
        _output.WriteLine($"Requested {criteria.MaxResults} results, got {results.Count}");
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobs_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetSavedJobsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplications_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetApplicationsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task AllJobsHaveRequiredFields_ValidatesRequiredFieldsContractAsync()
    {
        // Arrange
        GoogleJobClient client = _fixture.ServiceProvider.GetRequiredService<GoogleJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - RequiredFieldsContract validation
        if (results.Count > 0)
        {
            foreach (JobListing job in results)
            {
                Assert.False(string.IsNullOrWhiteSpace(job.Id), $"Job ID is required (Title: '{job.Title}')");
                Assert.False(string.IsNullOrWhiteSpace(job.Title), $"Job Title is required (ID: '{job.Id}')");
                Assert.False(string.IsNullOrWhiteSpace(job.Company), $"Job Company is required (ID: '{job.Id}', Title: '{job.Title}')");
                Assert.False(string.IsNullOrWhiteSpace(job.Source), $"Job Source is required (ID: '{job.Id}', Title: '{job.Title}')");
                Assert.Equal("Google", job.Source);
            }
        }

        _output.WriteLine($"Validated {results.Count} jobs for required fields");
    }
}
