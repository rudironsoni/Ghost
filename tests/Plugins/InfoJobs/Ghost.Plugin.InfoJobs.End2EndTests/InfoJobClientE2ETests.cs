using Ghost.Contracts.Jobs;
using Ghost.Plugin.InfoJobs.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

    public InfoJobClientE2ETests(InfoJobsE2EFixture fixture)
    {
        _fixture = fixture;
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

        JobListing firstJob = results[0];
        Assert.NotNull(firstJob.Id);
        Assert.Equal("InfoJobs", firstJob.Source);
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
}
