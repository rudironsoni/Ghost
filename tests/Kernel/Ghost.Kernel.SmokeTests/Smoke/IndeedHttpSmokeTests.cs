using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// HTTP-based smoke tests for Indeed platform.
/// Tests the Ghost API endpoints for Indeed job search and retrieval.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Plugin", "Indeed")]
public class IndeedHttpSmokeTests : ReliabilityTestBase, IClassFixture<GhostWebApiFixture>
{
    private readonly GhostWebApiFixture _fixture;

    public IndeedHttpSmokeTests(GhostWebApiFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
    }

    [ConditionalFact("Indeed")]
    public async Task SearchJobs_Returns_Populated_Fresh_Data()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 10,
            platform = "Indeed"
        };

        // Act
        Output.WriteLine($"Searching Indeed via API for: {searchRequest.query}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results!.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate freshness for all jobs
        foreach (JobListing job in results)
        {
            job.AssertFreshData(TimeSpan.FromDays(90));
        }

        // Output sample data for human verification
        Output.WriteLine("\n=== Sample Job Data ===");
        JobListing sampleJob = results[0]!;
        Output.WriteLine($"ID: {sampleJob.Id}");
        Output.WriteLine($"Title: {sampleJob.Title}");
        Output.WriteLine($"Company: {sampleJob.Company}");
        Output.WriteLine($"Location: {sampleJob.Location}");
        Output.WriteLine($"URL: {sampleJob.Url}");
        Output.WriteLine($"Posted: {sampleJob.PostedAt:yyyy-MM-dd}");
        Output.WriteLine($"Source: {sampleJob.Source}");
    }

    [ConditionalFact("Indeed")]
    public async Task SearchJobs_WithLocation_Returns_Jobs_In_Location()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            location = "Remote",
            maxResults = 10,
            platform = "Indeed"
        };

        // Act
        Output.WriteLine($"Searching Indeed via API for: {searchRequest.query} in {searchRequest.location}");
        List<JobListing>? results = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results!.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that jobs have location information
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        jobsWithLocation.Should().NotBeEmpty("at least some jobs should have location information");

        // Output sample locations for human verification
        Output.WriteLine("\n=== Sample Locations ===");
        foreach (JobListing? job in results.Take(3))
        {
            Output.WriteLine($"{job.Title} at {job.Company}: {job.Location ?? "No location"}");
        }
    }

    [ConditionalFact("Indeed")]
    public async Task GetJobDetails_ById_Returns_Valid_Data()
    {
        // Arrange
        var searchRequest = new
        {
            query = "software engineer",
            maxResults = 5,
            platform = "Indeed"
        };

        // First, search for a job to get a valid ID
        List<JobListing>? searchResults = await _fixture.PostAsync<object, List<JobListing>>(
            "/api/jobs/search",
            searchRequest,
            Output);

        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        string jobId = searchResults![0].Id;
        Output.WriteLine($"Testing GetJobDetails for job ID: {jobId}");

        // Act
        JobListing? jobDetails = await _fixture.GetAsync<JobListing>($"/api/jobs/{jobId}", Output);

        // Assert
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails!.Id.Should().Be(jobId, "job ID should match the requested ID");
        jobDetails.Source.Should().Be("Indeed", "source should be Indeed");

        // Validate required fields
        jobDetails.AssertRequiredFields();
        jobDetails.AssertValidPlatformId("Indeed");
        jobDetails.AssertUrlReachable();

        // Output detailed job information
        Output.WriteLine("\n=== Job Details ===");
        Output.WriteLine($"ID: {jobDetails.Id}");
        Output.WriteLine($"Title: {jobDetails.Title}");
        Output.WriteLine($"Company: {jobDetails.Company}");
        Output.WriteLine($"Location: {jobDetails.Location}");
        Output.WriteLine($"URL: {jobDetails.Url}");
        Output.WriteLine($"Posted: {jobDetails.PostedAt:yyyy-MM-dd}");
        Output.WriteLine($"Description Length: {jobDetails.Description?.Length ?? 0} characters");
        Output.WriteLine($"Job Type: {jobDetails.JobType}");
        Output.WriteLine($"Experience Level: {jobDetails.ExperienceLevel}");
        Output.WriteLine($"Easy Apply: {jobDetails.IsEasyApply}");
        Output.WriteLine($"Salary: {jobDetails.Salary ?? "Not specified"}");
    }
}
