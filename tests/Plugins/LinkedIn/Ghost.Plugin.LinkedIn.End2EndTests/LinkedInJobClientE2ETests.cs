using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Ghost.Testing.Contracts;
using Ghost.Testing.Contracts.BuiltIn;
using Ghost.Testing.End2End;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn Job Client using real browser sessions.
/// Tests full request/response lifecycle with actual GhostKernel browser sessions.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
[Trait("Capability", "RequiresProviderLive")]
public sealed class LinkedInJobClientE2ETests : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private readonly ITestOutputHelper _output;
    private LinkedInE2EFixture? _linkedInFixture;
    private IServiceProvider? _serviceProvider;

    public LinkedInJobClientE2ETests(RealBrowserFixture browserFixture, ITestOutputHelper output)
    {
        _browserFixture = browserFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _linkedInFixture = new LinkedInE2EFixture(_browserFixture);
        await _linkedInFixture.InitializeAsync();
        _serviceProvider = _linkedInFixture.ServiceProvider;
    }

    public async Task DisposeAsync()
    {
        if (_linkedInFixture != null)
        {
            await _linkedInFixture.DisposeAsync();
        }
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WhenKeywordsProvidedAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software Engineer",
            Location = "San Francisco, CA",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Basic null/empty checks
        Assert.NotNull(results);
        _output.WriteLine($"Found {results.Count} jobs");

        // Assert - Job count > 0
        Assert.True(results.Count > 0, "Expected at least one job to be returned");

        // Assert - Source == "LinkedIn"
        Assert.All(results, job => Assert.Equal("LinkedIn", job.Source));

        // Assert - RequiredFieldsContract validation
        await ValidateRequiredFieldsAsync(results);

        // Assert - Data quality checks
        await ValidateDataQualityAsync(results);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_ReturnsCompleteJob_WhenValidUrlProvidedAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();
        string jobId = "linkedin-job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert - Basic null check
        Assert.NotNull(result);

        // Assert - Required fields are populated
        Assert.False(string.IsNullOrWhiteSpace(result.Id), "Job ID should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(result.Title), "Job title should not be empty");

        // Assert - Source is LinkedIn
        Assert.Equal("LinkedIn", result.Source);

        // Assert - URL validation
        if (!string.IsNullOrWhiteSpace(result.Url))
        {
            Assert.True(IsValidUrl(result.Url), $"Invalid URL: {result.Url}");
        }

        // Assert - PostedAt is reasonable (not in the future, not too old)
        Assert.True(result.PostedAt <= DateTimeOffset.UtcNow, "PostedAt should not be in the future");
        Assert.True(result.PostedAt > DateTimeOffset.UtcNow.AddYears(-1), "PostedAt should be within the last year");

        _output.WriteLine($"Job details - ID: {result.Id}, Title: {result.Title}, Company: {result.Company}");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_RespectsMaxResultsAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();
        const int maxResults = 5;
        var criteria = new JobSearchCriteria
        {
            Query = "Developer",
            Location = "Remote",
            MaxResults = maxResults
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.Count <= maxResults, $"Expected at most {maxResults} results, but got {results.Count}");

        _output.WriteLine($"Requested {maxResults} results, got {results.Count}");
    }

    [Fact(Skip = "Application functionality requires external infrastructure")]
    [Trait("TestType", "End2End")]
    public async Task ApplyForJobAsync_ThrowsNotImplementedAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();
        string jobId = "linkedin-job-001";
        var details = new ApplicationDetails
        {
            ApplicantEmail = "test@example.com",
            ResumeUrl = "resume.pdf",
            CoverLetter = "Test cover letter"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ApplyAsync(jobId, details));
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task PlatformName_ReturnsLinkedInAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("LinkedIn", platformName);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_RequiredFieldsContract_PassesAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Engineer",
            Location = "United States",
            MaxResults = 10
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Run RequiredFieldsContract manually
        var contract = new RequiredFieldsContract();
        var adapter = new LinkedInContractAdapter(client);
        ContractResult contractResult = await contract.ExecuteAsync(adapter);

        Assert.True(contractResult.Passed, $"RequiredFieldsContract failed: {string.Join(", ", contractResult.Errors)}");
        _output.WriteLine($"RequiredFieldsContract passed. Context: {System.Text.Json.JsonSerializer.Serialize(contractResult.Context)}");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetSavedJobsAsync_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetSavedJobsAsync());
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetApplicationsAsync_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        LinkedInJobClient client = _serviceProvider!.GetRequiredService<LinkedInJobClient>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetApplicationsAsync());
    }

    #region Helper Methods

    private static async Task ValidateRequiredFieldsAsync(IReadOnlyList<JobListing> jobs)
    {
        List<string> errors = [];

        foreach (JobListing job in jobs)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(job.Id))
            {
                errors.Add($"Job missing required field: Id (Title: '{job.Title}')");
            }

            if (string.IsNullOrWhiteSpace(job.Title))
            {
                errors.Add($"Job missing required field: Title (Id: '{job.Id}')");
            }

            if (string.IsNullOrWhiteSpace(job.Company))
            {
                errors.Add($"Job missing required field: Company (Id: '{job.Id}', Title: '{job.Title}')");
            }

            // Validate optional but important fields
            if (string.IsNullOrWhiteSpace(job.Url))
            {
                errors.Add($"Job missing recommended field: Url (Id: '{job.Id}', Title: '{job.Title}')");
            }

            if (string.IsNullOrWhiteSpace(job.Source))
            {
                errors.Add($"Job missing recommended field: Source (Id: '{job.Id}', Title: '{job.Title}')");
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Required field validation failed:\n{string.Join("\n", errors)}");
        }

        await Task.CompletedTask;
    }

    private static async Task ValidateDataQualityAsync(IReadOnlyList<JobListing> jobs)
    {
        foreach (JobListing job in jobs)
        {
            // Validate URL is valid if present
            if (!string.IsNullOrWhiteSpace(job.Url) && !IsValidUrl(job.Url))
            {
                Assert.Fail($"Invalid URL for job '{job.Id}': {job.Url}");
            }

            // Validate dates are reasonable
            if (job.PostedAt > DateTimeOffset.UtcNow)
            {
                Assert.Fail($"PostedAt is in the future for job '{job.Id}': {job.PostedAt}");
            }

            if (job.PostedAt < DateTimeOffset.UtcNow.AddYears(-5))
            {
                Assert.Fail($"PostedAt is too old for job '{job.Id}': {job.PostedAt}");
            }

            // Validate text fields are not empty
            Assert.False(string.IsNullOrWhiteSpace(job.Title), $"Job '{job.Id}' has empty title");
            Assert.False(string.IsNullOrWhiteSpace(job.Company), $"Job '{job.Id}' has empty company");
        }

        await Task.CompletedTask;
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    #endregion

    #region LinkedInContractAdapter

    /// <summary>
    /// Contract adapter for LinkedIn provider contract testing.
    /// </summary>
    private sealed class LinkedInContractAdapter : IProviderContractAdapter
    {
        private readonly LinkedInJobClient _client;

        public LinkedInContractAdapter(LinkedInJobClient client)
        {
            _client = client;
        }

        public string PlatformName => "LinkedIn";

        public Task<IReadOnlyList<JobListing>> GetJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
        {
            return _client.SearchJobsAsync(criteria, ct);
        }

        public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
        {
            return _client.GetJobDetailsAsync(jobId, ct);
        }

        public async Task<IReadOnlyList<JobListing>> SearchWithPaginationAsync(
            JobSearchCriteria criteria,
            int maxPages = 10,
            CancellationToken ct = default)
        {
            // LinkedIn client doesn't support pagination directly, return all results
            IReadOnlyList<JobListing> results = await _client.SearchJobsAsync(criteria, ct);
            return results;
        }

        public Task<IReadOnlyList<JobListing>> TestRetryBehaviorAsync(
            JobSearchCriteria criteria,
            CancellationToken ct = default)
        {
            // Default implementation - just return normal results
            return _client.SearchJobsAsync(criteria, ct);
        }

        public Task<IReadOnlyList<JobListing>> TestConsentFlowAsync(
            JobSearchCriteria criteria,
            CancellationToken ct = default)
        {
            // Default implementation - just return normal results
            return _client.SearchJobsAsync(criteria, ct);
        }

        public async Task<(IReadOnlyList<JobListing> First, IReadOnlyList<JobListing> Second)> TestIdempotencyAsync(
            JobSearchCriteria criteria,
            CancellationToken ct = default)
        {
            IReadOnlyList<JobListing> first = await _client.SearchJobsAsync(criteria, ct);
            IReadOnlyList<JobListing> second = await _client.SearchJobsAsync(criteria, ct);
            return (first, second);
        }
    }

    #endregion
}
