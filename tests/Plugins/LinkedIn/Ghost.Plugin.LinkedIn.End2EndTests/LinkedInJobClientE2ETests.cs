using Ghost.Contracts.Jobs;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn Job Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInJobClientE2ETests
{
    private readonly LinkedInE2EFixture _fixture;

    public LinkedInJobClientE2ETests(LinkedInE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_WithValidCriteria_ReturnsJobListings()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();
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
        Assert.Equal("LinkedIn", client.PlatformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_WithValidJobId_ReturnsJobDetails()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();
        string jobId = "linkedin-job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("LinkedIn", result.Source);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("LinkedIn", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobsAsync_ThrowsNotImplementedException()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetSavedJobsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetApplicationsAsync_ThrowsNotImplementedException()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetApplicationsAsync());
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task ApplyAsync_WithValidJobId_ReturnsJobApplication()
    {
        // Arrange
        LinkedInJobClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInJobClient>();
        string jobId = "linkedin-job-001";
        var details = new ApplicationDetails
        {
            ApplicantEmail = "test@example.com",
            Resume = "resume.pdf",
            CoverLetter = "Test cover letter"
        };

        // Act
        JobApplication result = await client.ApplyAsync(jobId, details);

        // Assert - May return null or mock based on browser interaction
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("Applied", result.Status);
    }
}
