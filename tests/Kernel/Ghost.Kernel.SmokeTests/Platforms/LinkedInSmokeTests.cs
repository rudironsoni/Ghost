using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Integration;

/// <summary>
/// Integration tests for LinkedIn platform.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Platform", "LinkedIn")]
public class LinkedInIntegrationTests : ReliabilityTestBase, IClassFixture<PlatformIntegrationTestFixture>
{
    private readonly PlatformIntegrationTestFixture _fixture;
    private readonly IJobClient _client;

    public LinkedInIntegrationTests(PlatformIntegrationTestFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
        _client = _fixture.GetJobClient("linkedin");
    }

    [ConditionalFact("LinkedIn")]
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
        Output.WriteLine($"Searching LinkedIn for: {criteria.Query}");
        IReadOnlyList<JobListing> results = await _client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results.Count} jobs");

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
        JobListing sampleJob = results[0];
        Output.WriteLine($"ID: {sampleJob.Id}");
        Output.WriteLine($"Title: {sampleJob.Title}");
        Output.WriteLine($"Company: {sampleJob.Company}");
        Output.WriteLine($"Location: {sampleJob.Location}");
        Output.WriteLine($"URL: {sampleJob.Url}");
        Output.WriteLine($"Posted: {sampleJob.PostedAt:yyyy-MM-dd}");
        Output.WriteLine($"Source: {sampleJob.Source}");
    }

    [ConditionalFact("LinkedIn")]
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
        Output.WriteLine($"Searching LinkedIn for: {criteria.Query} in {criteria.Location}");
        IReadOnlyList<JobListing> results = await _client.SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("search results should not be null");
        results.Should().NotBeEmpty("search should return at least one job");

        Output.WriteLine($"Found {results.Count} jobs");

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

    [ConditionalFact("LinkedIn")]
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
        IReadOnlyList<JobListing> searchResults = await _client.SearchJobsAsync(searchCriteria, searchCts.Token);
        searchResults.Should().NotBeEmpty("need at least one job to test details endpoint");

        string jobId = searchResults[0].Id;
        Output.WriteLine($"Testing GetJobDetails for job ID: {jobId}");

        var detailsCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        JobListing jobDetails = await _client.GetJobDetailsAsync(jobId, detailsCts.Token);

        // Assert
        jobDetails.Should().NotBeNull("job details should not be null");
        jobDetails.Id.Should().Be(jobId, "job ID should match the requested ID");
        jobDetails.Source.Should().Be("LinkedIn", "source should be LinkedIn");

        // Validate required fields
        jobDetails.AssertRequiredFields();
        jobDetails.AssertValidPlatformId("LinkedIn");
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
