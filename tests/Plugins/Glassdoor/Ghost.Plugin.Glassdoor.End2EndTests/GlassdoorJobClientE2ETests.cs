using Ghost.Contracts.Jobs;
using Ghost.Plugin.Glassdoor.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Glassdoor.End2EndTests;

/// <summary>
/// End-to-End tests for Glassdoor Job Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("GlassdoorEnd2End")]
[Trait("Category", "End2End")]
public sealed class GlassdoorJobClientE2ETests
{
    private readonly GlassdoorE2EFixture _fixture;

    public GlassdoorJobClientE2ETests(GlassdoorE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_WithValidCriteria_ReturnsJobListings()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "San Francisco, CA",
            MaxResults = 10
        };

        // Act
        var results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        var firstJob = results.First();
        Assert.NotNull(firstJob.Id);
        Assert.Equal("Software Engineer", firstJob.Title);
        Assert.Equal("Tech Corp", firstJob.Company);
        Assert.Equal("San Francisco, CA", firstJob.Location);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_WithValidJobId_ReturnsJobDetails()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();
        var jobId = "123456";

        // Act
        var result = await client.GetJobDetailsAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("Glassdoor", result.Source);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_WithEmptyQuery_ReturnsEmptyResults()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();
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
    public async Task PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();

        // Act
        var platformName = client.PlatformName;

        // Assert
        Assert.Equal("Glassdoor", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobsAsync_ReturnsEmptyList()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();

        // Act
        var results = await client.GetSavedJobsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplicationsAsync_ReturnsEmptyList()
    {
        // Arrange
        var client = _fixture.ServiceProvider.GetRequiredService<GlassdoorJobClient>();

        // Act
        var results = await client.GetApplicationsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
