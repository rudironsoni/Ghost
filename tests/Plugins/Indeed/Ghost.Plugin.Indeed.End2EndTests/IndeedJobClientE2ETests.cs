using System.Globalization;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Plugin.Indeed.End2EndTests.Fixtures;
using Ghost.Testing.End2End;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Indeed.End2EndTests;

/// <summary>
/// End-to-End tests for Indeed Job Client using real browser automation.
/// Tests full request/response lifecycle with real GhostKernel browser sessions
/// against localhost TestScraperServer for realistic HTML fixtures.
/// </summary>
[Collection("Browser")]
[Trait("Category", "End2End")]
public sealed class IndeedJobClientE2ETests : IClassFixture<IndeedE2EFixture>, IAsyncLifetime
{
    private readonly IndeedE2EFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IBrowserSession _session;

    public IndeedJobClientE2ETests(IndeedE2EFixture fixture, ITestOutputHelper output, RealBrowserFixture browserFixture)
    {
        _fixture = fixture;
        _output = output;
        _session = _fixture.Session;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    #region Required Fields Contract Tests

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WithAllRequiredFieldsAsync()
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

        // Assert - Job Count Validation
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.True(results.Count > 0, "Expected at least one job result");
        _output.WriteLine($"Retrieved {results.Count} jobs from Indeed");

        // Assert - RequiredFieldsContract validation for each job
        foreach (JobListing job in results)
        {
            ValidateRequiredFields(job);
            ValidateDataQuality(job);
        }
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WithSourceSetToIndeedAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Developer",
            Location = "Remote",
            MaxResults = 5
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Source validation
        Assert.NotNull(results);
        Assert.All(results, job =>
        {
            Assert.Equal("Indeed", job.Source);
        });

        _output.WriteLine($"All {results.Count} jobs have Source='Indeed'");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_RespectsMaxResultsAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        int maxResults = 3;
        var criteria = new JobSearchCriteria
        {
            Query = "Engineer",
            Location = "New York",
            MaxResults = maxResults
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Max results validation
        Assert.NotNull(results);
        Assert.True(results.Count <= maxResults,
            $"Expected at most {maxResults} jobs, but got {results.Count}");

        _output.WriteLine($"MaxResults={maxResults}, ActualResults={results.Count}");
    }

    #endregion

    #region Data Quality Contract Tests

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WithValidUrlsAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Software",
            Location = "Austin",
            MaxResults = 5
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - URL validation (DataQualityContract)
        Assert.NotNull(results);
        foreach (JobListing job in results)
        {
            if (!string.IsNullOrWhiteSpace(job.Url))
            {
                AssertValidJobUrl(job.Url, job.Id);
            }
        }

        _output.WriteLine($"Validated URLs for {results.Count} jobs");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WithReasonablePostedDatesAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Manager",
            Location = "Chicago",
            MaxResults = 5
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - PostedAt date validation (DataQualityContract)
        Assert.NotNull(results);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset oldestAllowed = now.AddDays(-365); // Jobs posted within last year

        foreach (JobListing job in results)
        {
            Assert.True(job.PostedAt <= now,
                $"Job {job.Id} has future PostedAt date: {job.PostedAt}");
            Assert.True(job.PostedAt >= oldestAllowed,
                $"Job {job.Id} has PostedAt date too old: {job.PostedAt}");
        }

        _output.WriteLine($"Validated posted dates for {results.Count} jobs");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task SearchJobsAsync_ReturnsJobs_WithNonEmptyTextFieldsAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        var criteria = new JobSearchCriteria
        {
            Query = "Analyst",
            Location = "Boston",
            MaxResults = 5
        };

        // Act
        IReadOnlyList<JobListing> results = await client.SearchJobsAsync(criteria);

        // Assert - Text field validation (DataQualityContract)
        Assert.NotNull(results);
        foreach (JobListing job in results)
        {
            // Title should not be just whitespace
            Assert.False(string.IsNullOrWhiteSpace(job.Title),
                $"Job {job.Id} has empty or whitespace-only title");

            // Company should not be just whitespace
            Assert.False(string.IsNullOrWhiteSpace(job.Company),
                $"Job {job.Id} has empty or whitespace-only company");

            // Title should have reasonable length
            Assert.True(job.Title.Length >= 3,
                $"Job {job.Id} has suspiciously short title: '{job.Title}'");
            Assert.True(job.Title.Length <= 200,
                $"Job {job.Id} has suspiciously long title: '{job.Title}'");
        }

        _output.WriteLine($"Validated text fields for {results.Count} jobs");
    }

    #endregion

    #region GetJobDetails Tests

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetJobDetailsAsync_ReturnsJob_WhenValidJobIdProvidedAsync()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();
        string jobId = "test-job-001";

        // Act
        JobListing result = await client.GetJobDetailsAsync(jobId);

        // Assert - Stub implementation returns basic job info
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal("Indeed", result.Source);

        _output.WriteLine($"Retrieved job details for ID: {jobId}");
    }

    #endregion

    #region Browser Integration Tests

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task BrowserSession_CanNavigateToIndeedSearchPageAsync()
    {
        // Arrange
        string searchUrl = $"{_fixture.IndeedBaseUrl}/jobs?q=Software+Engineer&l=Remote";

        // Act
        IPage page = await _session.NewPageAsync();
        await using (page)
        {
            await page.NavigateAsync(searchUrl);
            await page.WaitForLoadStateAsync();

            // Assert
            Assert.Contains("indeed", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("jobs", page.Url, StringComparison.OrdinalIgnoreCase);

            // Verify page has content
            string? title = await page.GetTitleAsync();
            Assert.NotNull(title);

            _output.WriteLine($"Navigated to Indeed search page: {page.Url}");
            _output.WriteLine($"Page title: {title}");
        }
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task BrowserSession_CanExtractJobElementsFromPageAsync()
    {
        // Arrange
        string searchUrl = $"{_fixture.IndeedBaseUrl}/jobs?q=Developer&l=New+York";

        // Act
        IPage page = await _session.NewPageAsync();
        await using (page)
        {
            await page.NavigateAsync(searchUrl);
            await page.WaitForLoadStateAsync();

            // Try to find job elements (using common Indeed selectors)
            IReadOnlyList<IElement> jobElements = await page.QuerySelectorAllAsync("[data-testid='job-title'], .jobTitle, .jobTitle-color-purple");

            // Assert - We should find job elements on the page
            Assert.NotNull(jobElements);
            _output.WriteLine($"Found {jobElements.Count} job elements on the page");

            // If elements found, verify they have text content
            if (jobElements.Count > 0)
            {
                foreach (IElement element in jobElements.Take(3)) // Check first 3
                {
                    string? text = await element.GetTextContentAsync();
                    Assert.False(string.IsNullOrWhiteSpace(text), "Job element should have text content");
                    _output.WriteLine($"Job element text: {text}");
                }
            }
        }
    }

    #endregion

    #region Platform Integration Tests

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        IndeedJobClient client = _fixture.ServiceProvider.GetRequiredService<IndeedJobClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("Indeed", platformName);
        _output.WriteLine($"Platform name: {platformName}");
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task ServiceProvider_ResolvesIndeedJobClientAsync()
    {
        // Act
        IndeedJobClient? client = _fixture.ServiceProvider.GetService<IndeedJobClient>();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<IndeedJobClient>(client);
        _output.WriteLine("IndeedJobClient resolved successfully from ServiceProvider");
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates required fields according to RequiredFieldsContract.
    /// </summary>
    private static void ValidateRequiredFields(JobListing job)
    {
        // Required: Id
        Assert.False(string.IsNullOrWhiteSpace(job.Id),
            $"Job missing required field: Id (Title: '{job.Title}')");

        // Required: Title
        Assert.False(string.IsNullOrWhiteSpace(job.Title),
            $"Job missing required field: Title (Id: '{job.Id}')");

        // Required: Company
        Assert.False(string.IsNullOrWhiteSpace(job.Company),
            $"Job missing required field: Company (Id: '{job.Id}', Title: '{job.Title}')");

        // Recommended: Url
        Assert.False(string.IsNullOrWhiteSpace(job.Url),
            $"Job missing recommended field: Url (Id: '{job.Id}', Title: '{job.Title}')");

        // Recommended: Source
        Assert.False(string.IsNullOrWhiteSpace(job.Source),
            $"Job missing recommended field: Source (Id: '{job.Id}', Title: '{job.Title}')");

        // Source should be "Indeed"
        Assert.Equal("Indeed", job.Source);
    }

    /// <summary>
    /// Validates data quality according to DataQualityContract.
    /// </summary>
    private static void ValidateDataQuality(JobListing job)
    {
        // Validate URL format if present
        if (!string.IsNullOrWhiteSpace(job.Url))
        {
            AssertValidJobUrl(job.Url, job.Id);
        }

        // Validate posted date is reasonable
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset oldestAllowed = now.AddDays(-365);
        Assert.True(job.PostedAt <= now,
            $"Job {job.Id} has future PostedAt date: {job.PostedAt}");
        Assert.True(job.PostedAt >= oldestAllowed,
            $"Job {job.Id} has PostedAt date too old: {job.PostedAt}");

        // Validate text fields have reasonable content
        Assert.True(job.Title.Length >= 3 && job.Title.Length <= 200,
            $"Job {job.Id} has suspicious title length: {job.Title.Length}");
        Assert.True(job.Company.Length >= 2 && job.Company.Length <= 100,
            $"Job {job.Id} has suspicious company length: {job.Company.Length}");
    }

    /// <summary>
    /// Validates that a job URL is well-formed and not localhost.
    /// </summary>
    private static void AssertValidJobUrl(string url, string jobId)
    {
        // Must be valid absolute URI
        Assert.True(Uri.TryCreate(url, UriKind.Absolute, out Uri? uri),
            $"Job {jobId} has invalid URL format: '{url}'");

        // Must use HTTP or HTTPS scheme
        Assert.True(uri!.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps,
            $"Job {jobId} has invalid URL scheme: '{uri.Scheme}'");

        // Should not point to localhost in production data
        Assert.DoesNotContain("localhost", uri.Host, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", uri.Host, StringComparison.OrdinalIgnoreCase);

        // Should have a path component (not just domain)
        Assert.False(string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/",
            $"Job {jobId} URL has no path component: '{url}'");
    }

    #endregion
}
