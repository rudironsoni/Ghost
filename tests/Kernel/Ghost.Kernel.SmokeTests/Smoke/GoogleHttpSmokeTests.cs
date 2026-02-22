using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// HTTP-based smoke tests for Google platform.
/// Tests the Ghost API endpoints for Google job search and retrieval.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Plugin", "Google")]
public class GoogleHttpSmokeTests : IClassFixture<GhostWebApiFixture>
{
    private readonly GhostWebApiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GoogleHttpSmokeTests(GhostWebApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact(Skip = "Google Jobs integration unstable in test environment")]
    public async Task SearchJobs_Returns_Populated_Fresh_Data()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 10,
            platform = "Google"
        };

        // Act
        _output.WriteLine($"Searching Google via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results!.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate freshness for all jobs
        foreach (JobListing job in results)
        {
            job.AssertFreshData(TimeSpan.FromDays(90));
        }

        // Output sample data for human verification
        _output.WriteLine("\n=== Sample Job Data ===");
        JobListing sampleJob = results[0]!;
        _output.WriteLine($"ID: {sampleJob.Id}");
        _output.WriteLine($"Title: {sampleJob.Title}");
        _output.WriteLine($"Company: {sampleJob.Company}");
        _output.WriteLine($"Location: {sampleJob.Location}");
        _output.WriteLine($"URL: {sampleJob.Url}");
        _output.WriteLine($"Posted: {sampleJob.PostedAt:yyyy-MM-dd}");
        _output.WriteLine($"Source: {sampleJob.Source}");
    }

    [Fact(Skip = "Google Jobs integration unstable in test environment")]
    public async Task SearchJobs_WithLocation_Returns_Jobs_In_Location()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            location = "Remote",
            maxResults = 10,
            platform = "Google"
        };

        // Act
        _output.WriteLine($"Searching Google via API for: {searchRequest.query} in {searchRequest.location}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results!.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that jobs have location information
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        jobsWithLocation.Should().NotBeEmpty("at least some jobs should have location information");

        // Output sample locations for human verification
        _output.WriteLine("\n=== Sample Locations ===");
        foreach (JobListing? job in results.Take(3))
        {
            _output.WriteLine($"{job.Title} at {job.Company}: {job.Location ?? "No location"}");
        }
    }

    [Fact(Skip = "Google Jobs integration unstable in test environment")]
    public async Task GetJobDetails_ById_Returns_Valid_Data()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 5,
            platform = "Google"
        };

        // First, search for a job to get a valid ID
        List<JobListing>? searchResults = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            _output);

        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        string jobId = searchResults![0].Id;
        _output.WriteLine($"Testing GetJobDetails for job ID: {jobId}");

        // Act
        JobListing? jobDetails = await _fixture.GetAsync<JobListing>($"/api/jobs/{jobId}", _output);

        // Assert
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails!.Id.Should().Be(jobId, "job ID should match the requested ID");
        jobDetails.Source.Should().Be("Google", "source should be Google");

        // Validate required fields
        jobDetails.AssertRequiredFields();
        jobDetails.AssertValidPlatformId("Google");
        jobDetails.AssertUrlReachable();

        // Output detailed job information
        _output.WriteLine("\n=== Job Details ===");
        _output.WriteLine($"ID: {jobDetails.Id}");
        _output.WriteLine($"Title: {jobDetails.Title}");
        _output.WriteLine($"Company: {jobDetails.Company}");
        _output.WriteLine($"Location: {jobDetails.Location}");
        _output.WriteLine($"URL: {jobDetails.Url}");
        _output.WriteLine($"Posted: {jobDetails.PostedAt:yyyy-MM-dd}");
        _output.WriteLine($"Description Length: {jobDetails.Description?.Length ?? 0} characters");
        _output.WriteLine($"Job Type: {jobDetails.JobType}");
        _output.WriteLine($"Experience Level: {jobDetails.ExperienceLevel}");
        _output.WriteLine($"Easy Apply: {jobDetails.IsEasyApply}");
        _output.WriteLine($"Salary: {jobDetails.Salary ?? "Not specified"}");
    }
}
