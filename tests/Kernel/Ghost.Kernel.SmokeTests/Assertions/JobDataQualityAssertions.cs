using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Ghost.Contracts.Jobs;

namespace Ghost.Smoke.Tests.Assertions;

/// <summary>
/// Assertions for validating job data quality in smoke tests.
/// </summary>
public static class JobDataQualityAssertions
{
    private static readonly Regex PlatformIdPattern = new(@"^[a-zA-Z0-9]+-[a-zA-Z0-9\-]+$", RegexOptions.Compiled);
    private static readonly Regex UrlPattern = new(@"^https?://[^\s/$.?#].[^\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates all required fields are populated with real data.
    /// </summary>
    public static void AssertRealJobData(this JobListing job)
    {
        job.Should().NotBeNull("job listing must not be null");

        job.AssertRequiredFields();
        job.AssertValidPlatformId(job.Source ?? string.Empty);
        job.AssertUrlReachable();
    }

    /// <summary>
    /// Validates a collection of job results is non-empty and all items are valid.
    /// </summary>
    public static void AssertRealJobResults(this IEnumerable<JobListing> jobs)
    {
        jobs.Should().NotBeNull("job results collection must not be null");
        jobs.Should().NotBeEmpty("job results must contain at least one job");

        var jobList = jobs.ToList();
        jobList.Should().NotBeEmpty("job results must contain at least one job");

        foreach (JobListing? job in jobList)
        {
            job.AssertRealJobData();
        }
    }

    /// <summary>
    /// Validates the job's posted date is within the specified freshness window.
    /// </summary>
    /// <param name="job">The job listing to validate.</param>
    /// <param name="maxAge">Maximum age of the job (default: 90 days).</param>
    public static void AssertFreshData(this JobListing job, TimeSpan? maxAge = null)
    {
        job.Should().NotBeNull("job listing must not be null");

        TimeSpan age = maxAge ?? TimeSpan.FromDays(90);
        DateTimeOffset cutoffDate = DateTimeOffset.UtcNow.Subtract(age);

        job.PostedAt.Should().NotBe(default, "PostedAt must be set to a valid date");
        job.PostedAt.Should().BeAfter(cutoffDate,
            $"job data must be fresh (posted within {age.TotalDays} days). " +
            $"Job posted at: {job.PostedAt:yyyy-MM-dd}, " +
            $"Cutoff date: {cutoffDate:yyyy-MM-dd}, " +
            $"Job age: {(DateTimeOffset.UtcNow - job.PostedAt).TotalDays:F1} days");
    }

    /// <summary>
    /// Validates the job ID follows the expected platform format.
    /// </summary>
    /// <param name="job">The job listing to validate.</param>
    /// <param name="expectedPlatform">Expected platform name (e.g., "LinkedIn", "Indeed").</param>
    public static void AssertValidPlatformId(this JobListing job, string expectedPlatform)
    {
        job.Should().NotBeNull("job listing must not be null");

        job.Id.Should().NotBeNullOrEmpty("job Id must not be null or empty");

        string id = job.Id.Trim();
        id.Should().NotBeEmpty("job Id must not be empty or whitespace");

        id.Should().MatchRegex(PlatformIdPattern,
            $"job Id must follow format '{{platform}}-{{uniqueId}}'. " +
            $"Expected platform: '{expectedPlatform}', " +
            $"Actual Id: '{id}', " +
            $"Pattern: {{platform}}-{{alphanumeric}}");

        // Verify the platform prefix matches
        string platformPrefix = id.Split('-')[0];
        platformPrefix.Should().Be(expectedPlatform,
            $"job Id platform prefix must match expected platform. " +
            $"Expected: '{expectedPlatform}', " +
            $"Actual prefix: '{platformPrefix}', " +
            $"Full Id: '{id}'");
    }

    /// <summary>
    /// Validates there are no duplicate job IDs in the collection.
    /// </summary>
    public static void AssertNoDuplicateJobs(this IEnumerable<JobListing> jobs)
    {
        jobs.Should().NotBeNull("job results collection must not be null");

        var jobList = jobs.ToList();
        var duplicateIds = jobList
            .GroupBy(j => j.Id)
            .Where(g => g.Count() > 1)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();

        duplicateIds.Should().BeEmpty(
            $"job results must not contain duplicate IDs. " +
            $"Found {duplicateIds.Count} duplicate(s): " +
            $"{string.Join(", ", duplicateIds.Select(d => $"'{d.Id}' ({d.Count} occurrences)"))}");
    }

    /// <summary>
    /// Validates all required fields are populated.
    /// </summary>
    public static void AssertRequiredFields(this JobListing job)
    {
        job.Should().NotBeNull("job listing must not be null");

        // Validate Id
        job.Id.Should().NotBeNullOrEmpty("job Id must not be null or empty");
        job.Id.Should().NotBeNullOrWhiteSpace("job Id must not be whitespace only");

        // Validate Title
        job.Title.Should().NotBeNullOrEmpty("job Title must not be null or empty");
        job.Title.Should().NotBeNullOrWhiteSpace("job Title must not be whitespace only");
        job.Title.Length.Should().BeGreaterOrEqualTo(5,
            $"job Title must have reasonable length (5-200 chars). " +
            $"Actual length: {job.Title.Length}, " +
            $"Title: '{job.Title}'");
        job.Title.Length.Should().BeLessOrEqualTo(200,
            $"job Title must have reasonable length (5-200 chars). " +
            $"Actual length: {job.Title.Length}, " +
            $"Title: '{job.Title}'");

        // Validate Company
        job.Company.Should().NotBeNullOrEmpty("job Company must not be null or empty");
        job.Company.Should().NotBeNullOrWhiteSpace("job Company must not be whitespace only");

        // Validate Url
        job.Url.Should().NotBeNullOrEmpty("job Url must not be null or empty");
        job.Url.Should().NotBeNullOrWhiteSpace("job Url must not be whitespace only");
        job.Url.Should().MatchRegex(UrlPattern,
            $"job Url must be a valid absolute URL. " +
            $"Actual Url: '{job.Url}'");

        // Validate Source (PlatformName)
        job.Source.Should().NotBeNullOrEmpty("job Source must not be null or empty");
        job.Source.Should().NotBeNullOrWhiteSpace("job Source must not be whitespace only");
    }

    /// <summary>
    /// Validates the URL is in a valid format (does not make HTTP calls).
    /// </summary>
    public static void AssertUrlReachable(this JobListing job)
    {
        job.Should().NotBeNull("job listing must not be null");

        job.Url.Should().NotBeNullOrEmpty("job Url must not be null or empty");
        job.Url.Should().NotBeNullOrWhiteSpace("job Url must not be whitespace only");

        job.Url.Should().MatchRegex(UrlPattern,
            $"job Url must be a valid absolute URL format. " +
            $"Actual Url: '{job.Url}', " +
            $"Expected format: http://... or https://...");

        // Additional validation: URL should not be localhost or invalid schemes
        string lowerUrl = job.Url!.ToLowerInvariant();
        lowerUrl.Should().NotStartWith("http://localhost",
            "job Url should not point to localhost");
        lowerUrl.Should().NotStartWith("https://localhost",
            "job Url should not point to localhost");
        lowerUrl.Should().NotStartWith("file://",
            "job Url should not use file:// scheme");
    }
}
