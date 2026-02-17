using Ghost.Contracts.Jobs;
using Ghost.Plugin.InfoJobs.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.InfoJobs.End2EndTests;

/// <summary>
/// End-to-End tests for InfoJobs Job Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("InfoJobsEnd2End")]
[Trait("Category", "End2End")]
public sealed class InfoJobClientE2ETests
{
    private readonly InfoJobsE2EFixture _fixture;
    private readonly ITestOutputHelper _output;

    public InfoJobClientE2ETests(InfoJobsE2EFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_WithValidCriteria_ReturnsJobListingsAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Desarrollador",
            Location = "Madrid",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        JobListing firstJob = results[0];
        Assert.NotNull(firstJob.Id);
        Assert.Equal("InfoJobs", firstJob.Source);

        // RequiredFieldsContract validation
        Assert.False(string.IsNullOrWhiteSpace(firstJob.Title), "Job must have a title");
        Assert.False(string.IsNullOrWhiteSpace(firstJob.Company), "Job must have a company");
        Assert.False(string.IsNullOrWhiteSpace(firstJob.Source), "Job must have a source");

        // Data quality checks
        Assert.True(results.Count <= criteria.MaxResults, "Results should respect MaxResults");

        _output.WriteLine($"Found {results.Count} jobs from InfoJobs");
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetails_WithValidJobId_ReturnsJobDetailsAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        string jobId = "infojobs-job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("InfoJobs", result.Source);

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
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("InfoJobs", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobs_WithEmptyQuery_ReturnsResultsAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
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
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Desarrollador",
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
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetSavedJobsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplications_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetApplicationsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task Apply_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        string jobId = "infojobs-job-001";
        var details = new ApplicationDetails
        {
            ApplicantEmail = "test@example.com",
            ResumeUrl = "resume.pdf"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ApplyAsync(jobId, details));
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task AllJobsHaveRequiredFields_ValidatesRequiredFieldsContractAsync()
    {
        // Arrange
        InfoJobClient client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Desarrollador",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - RequiredFieldsContract validation
        Assert.NotEmpty(results);

        foreach (JobListing job in results)
        {
            Assert.False(string.IsNullOrWhiteSpace(job.Id), $"Job ID is required (Title: '{job.Title}')");
            Assert.False(string.IsNullOrWhiteSpace(job.Title), $"Job Title is required (ID: '{job.Id}')");
            Assert.False(string.IsNullOrWhiteSpace(job.Company), $"Job Company is required (ID: '{job.Id}', Title: '{job.Title}')");
            Assert.False(string.IsNullOrWhiteSpace(job.Source), $"Job Source is required (ID: '{job.Id}', Title: '{job.Title}')");
            Assert.Equal("InfoJobs", job.Source);
        }

        _output.WriteLine($"Validated {results.Count} jobs for required fields");
    }
}
