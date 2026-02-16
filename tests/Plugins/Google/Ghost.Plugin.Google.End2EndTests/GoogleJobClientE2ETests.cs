using Ghost.Contracts.Jobs;
using Ghost.Plugin.Google.End2EndTests.Fixtures;
using Ghost.Plugin.Google.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests;

/// <summary>
/// End-to-End tests for Google Jobs Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("GoogleEnd2End")]
[Trait("Category", "End2End")]
public sealed class GoogleJobClientE2ETests
{
    private readonly GoogleE2EFixture _fixture;

    public GoogleJobClientE2ETests(GoogleE2EFixture fixture)
    {
        _fixture = fixture;
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

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        JobListing firstJob = results[0];
        Assert.NotNull(firstJob.Id);
        Assert.Equal("Google", firstJob.Source);
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
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task PlatformName_ReturnsExpectedValueAsync()
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
}
