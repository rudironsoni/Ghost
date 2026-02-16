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
    public async Task SearchJobsAsync_WithValidCriteria_ReturnsJobListings()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Desarrollador",
            Location = "Madrid",
            MaxResults = 10
        };

        // Act
        var results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);

        var firstJob = results.First();
        Assert.NotNull(firstJob.Id);
        Assert.Equal("InfoJobs", firstJob.Source);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_WithValidJobId_ReturnsJobDetails()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var jobId = "infojobs-job-001";

        // Act
        var result = await client.GetJobDetailsAsync(jobId);

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
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act
        var platformName = client.PlatformName;

        // Assert
        Assert.Equal("InfoJobs", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_WithEmptyQuery_ReturnsResults()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = string.Empty,
            Location = string.Empty,
            MaxResults = 10
        };

        // Act
        var results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobsAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetSavedJobsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplicationsAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetApplicationsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task ApplyAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<InfoJobClient>();
        var jobId = "infojobs-job-001";
        var details = new ApplicationDetails
        {
            ApplicantEmail = "test@example.com",
            Resume = "resume.pdf"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ApplyAsync(jobId, details));
    }
}
