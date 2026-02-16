using Ghost.Contracts.Jobs;
using Ghost.Plugin.Indeed.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Indeed.End2EndTests;

/// <summary>
/// End-to-End tests for Indeed Job Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("IndeedEnd2End")]
[Trait("Category", "End2End")]
public sealed class IndeedJobClientE2ETests
{
    private readonly IndeedE2EFixture _fixture;

    public IndeedJobClientE2ETests(IndeedE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_WithValidCriteria_ReturnsJobListings()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "San Francisco, CA",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        JobListing firstJob = results.First();
        Assert.NotNull(firstJob.Id);
        Assert.Equal("Indeed", firstJob.Source);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_WithValidJobId_ReturnsJobDetails()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        string jobId = "indeed-job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("Indeed", result.Source);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("Indeed", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobsAsync_ReturnsEmptyList()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();

        // Act
        IReadOnlyList<JobListing> results = await client.GetSavedJobsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplicationsAsync_ReturnsEmptyList()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();

        // Act
        IReadOnlyList<JobApplication> results = await client.GetApplicationsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task ApplyAsync_ReturnsJobApplication()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        string jobId = "indeed-job-001";
        var details = new ApplicationDetails
        {
            ApplicantEmail = "test@example.com",
            Resume = "resume.pdf",
            CoverLetter = "Cover letter text"
        };

        // Act
        JobApplication result = await client.ApplyAsync(jobId, details);

        // Assert
        Assert.NotNull(result);
    }
}
