using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Integration;

/// <summary>
/// Integration tests for Indeed platform.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Platform", "Indeed")]
public class IndeedIntegrationTests : IClassFixture<PlatformIntegrationTestFixture>
{
    private readonly PlatformIntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IJobClient _client;

    public IndeedIntegrationTests(PlatformIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _client = _fixture.GetJobClient("indeed");
    }

    [Fact]
    public async Task Search_RealJobs_Returns_Populated_Fresh_Data()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            MaxResults = 10
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        _output.WriteLine($"Searching Indeed for: {criteria.Query}");
        var results = await _client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate freshness for all jobs
        foreach (var job in results)
        {
            job.AssertFreshData(TimeSpan.FromDays(90));
        }

        // Output sample data for human verification
        _output.WriteLine("\n=== Sample Job Data ===");
        var sampleJob = results[0];
        _output.WriteLine($"ID: {sampleJob.Id}");
        _output.WriteLine($"Title: {sampleJob.Title}");
        _output.WriteLine($"Company: {sampleJob.Company}");
        _output.WriteLine($"Location: {sampleJob.Location}");
        _output.WriteLine($"URL: {sampleJob.Url}");
        _output.WriteLine($"Posted: {sampleJob.PostedAt:yyyy-MM-dd}");
        _output.WriteLine($"Source: {sampleJob.Source}");
    }

    [Fact]
    public async Task Search_WithLocation_Returns_Jobs_In_Location()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "Remote",
            MaxResults = 10
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        _output.WriteLine($"Searching Indeed for: {criteria.Query} in {criteria.Location}");
        var results = await _client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        _output.WriteLine($"Found {results.Count} jobs");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Validate that jobs have location information
        var jobsWithLocation = results.Where(j => !string.IsNullOrEmpty(j.Location)).ToList();
        jobsWithLocation.Should().NotBeEmpty("at least some jobs should have location information");

        // Output sample locations for human verification
        _output.WriteLine("\n=== Sample Locations ===");
        foreach (var job in results.Take(3))
        {
            _output.WriteLine($"{job.Title} at {job.Company}: {job.Location ?? "No location"}");
        }
    }

    [Fact]
    public async Task GetJobDetails_ById_Returns_Valid_Data()
    {
        // Arrange
        var searchCriteria = new JobSearchCriteria
        {
            Query = "software engineer",
            MaxResults = 5
        };
        var searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // First, search for a job to get a valid ID
        var searchResults = await _client.SearchJobsAsync(searchCriteria, searchCts.Token);
        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        var jobId = searchResults[0].Id;
        _output.WriteLine($"Testing GetJobDetails for job ID: {jobId}");

        var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var jobDetails = await _client.GetJobDetailsAsync(jobId, detailsCts.Token);

        // Assert
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails.Id.Should().Be(jobId, "job ID should match the requested ID");
        jobDetails.Source.Should().Be("Indeed", "source should be Indeed");

        // Validate required fields
        jobDetails.AssertRequiredFields();
        jobDetails.AssertValidPlatformId("Indeed");
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
