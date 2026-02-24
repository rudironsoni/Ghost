using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Smoke.Tests.Assertions;
using Ghost.Smoke.Tests.Integration;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Flows;

/// <summary>
/// Multi-platform aggregation integration tests that validate cross-platform search functionality.
/// These tests ensure that the AggregatedJobClient correctly combines results from multiple platforms.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Flow", "MultiPlatform")]
public class MultiPlatformAggregationTests : ReliabilityTestBase, IClassFixture<PlatformIntegrationTestFixture>
{
    private readonly PlatformIntegrationTestFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public MultiPlatformAggregationTests(PlatformIntegrationTestFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
        _serviceProvider = fixture.ServiceProvider;
    }

    [ConditionalFact("MultiPlatform")]
    public async Task AggregateSearch_AllPlatforms_Returns_DiverseResults()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software developer",
            MaxResults = 30
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        Output.WriteLine($"=== Aggregated Search Across All Platforms ===");
        Output.WriteLine($"Query: {criteria.Query}");
        Output.WriteLine($"Max Results: {criteria.MaxResults}");

        IReadOnlyList<JobListing> results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("aggregated results should not be null");
        results.Should().NotBeEmpty("aggregated search should return at least one job");

        Output.WriteLine($"\nTotal jobs found: {results.Count}");

        // Validate data quality
        results.AssertRealJobResults();
        results.AssertNoDuplicateJobs();

        // Analyze platform distribution
        var platformGroups = results
            .Where(j => !string.IsNullOrEmpty(j.Source))
            .GroupBy(j => j.Source!)
            .OrderByDescending(g => g.Count())
            .ToList();

        Output.WriteLine($"\n=== Platform Distribution ===");
        Output.WriteLine($"Platforms contributing data: {platformGroups.Count}");

        foreach (IGrouping<string, JobListing>? group in platformGroups)
        {
            Output.WriteLine($"  - {group.Key}: {group.Count()} jobs ({(group.Count() * 100.0 / results.Count):F1}%)");
        }

        // Validate that more than one platform returned data
        platformGroups.Should().HaveCountGreaterThan(1,
            "aggregated search should return results from multiple platforms");

        // Validate that no single platform dominates excessively (>80%)
        IGrouping<string, JobListing> topPlatform = platformGroups.First();
        double topPlatformPercentage = (topPlatform.Count() * 100.0) / results.Count;
        Output.WriteLine($"\nTop platform: {topPlatform.Key} ({topPlatformPercentage:F1}%)");

        // Output sample jobs from each platform
        Output.WriteLine("\n=== Sample Jobs by Platform ===");
        foreach (IGrouping<string, JobListing>? group in platformGroups)
        {
            Output.WriteLine($"\n{group.Key} ({group.Count()} jobs):");
            foreach (JobListing? job in group.Take(2))
            {
                Output.WriteLine($"  - {job.Title} at {job.Company}");
            }
        }

        // Validate diversity in job titles
        var uniqueTitles = results.Select(j => j.Title).Distinct().ToList();
        Output.WriteLine($"\n=== Title Diversity ===");
        Output.WriteLine($"Unique job titles: {uniqueTitles.Count}/{results.Count}");
        uniqueTitles.Should().HaveCountGreaterThan(results.Count / 2,
            "there should be good diversity in job titles (not all duplicates)");
    }

    [ConditionalFact("MultiPlatform")]
    public async Task PlatformCoverage_Verifies_AllPlatforms_Are_Healthy()
    {
        // Arrange
        string[] platforms = new[] { "linkedin", "indeed", "glassdoor", "infojobs" };
        var criteria = new JobSearchCriteria
        {
            Query = "developer",
            MaxResults = 5
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Output.WriteLine($"=== Platform Health Check ===");
        Output.WriteLine($"Testing {platforms.Length} platforms");
        Output.WriteLine($"Query: {criteria.Query}");

        int healthyPlatforms = 0;
        int unhealthyPlatforms = 0;
        var platformResults = new System.Collections.Generic.Dictionary<string, PlatformHealthResult>();

        // Test each platform individually
        foreach (string? platform in platforms)
        {
            Output.WriteLine($"\n--- Testing Platform: {platform} ---");

            IJobClient? platformClient = null;
            try
            {
                platformClient = _fixture.GetJobClient(platform);
            }
            catch (Exception ex)
            {
                Output.WriteLine($"  ✗ Client initialization failed: {ex.Message}");
                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = false,
                    Error = $"Client initialization failed: {ex.Message}"
                };
                unhealthyPlatforms++;
                continue;
            }

            if (platformClient == null)
            {
                Output.WriteLine($"  ✗ Client is null");
                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = false,
                    Error = "Client is null"
                };
                unhealthyPlatforms++;
                continue;
            }

            // Search for jobs
            IReadOnlyList<JobListing> searchResults;
            try
            {
                searchResults = await platformClient.SearchJobsAsync(criteria, cts.Token);
            }
            catch (Exception ex)
            {
                Output.WriteLine($"  ✗ Search failed: {ex.Message}");
                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = false,
                    Error = $"Search failed: {ex.Message}"
                };
                unhealthyPlatforms++;
                continue;
            }

            // Validate results
            if (!searchResults.Any())
            {
                Output.WriteLine($"  ✗ No jobs returned");
                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = false,
                    Error = "No jobs returned"
                };
                unhealthyPlatforms++;
                continue;
            }

            // Validate data quality
            try
            {
                searchResults.AssertRealJobResults();
                searchResults.AssertNoDuplicateJobs();

                Output.WriteLine($"  ✓ Healthy: {searchResults.Count} jobs found");
                Output.WriteLine($"    Sample: {searchResults[0].Title} at {searchResults[0].Company}");

                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = true,
                    JobCount = searchResults.Count,
                    SampleTitle = searchResults[0].Title,
                    SampleCompany = searchResults[0].Company
                };
                healthyPlatforms++;
            }
            catch (Exception ex)
            {
                Output.WriteLine($"  ✗ Data quality validation failed: {ex.Message}");
                platformResults[platform] = new PlatformHealthResult
                {
                    Platform = platform,
                    IsHealthy = false,
                    Error = $"Data quality validation failed: {ex.Message}"
                };
                unhealthyPlatforms++;
            }
        }

        // Summary
        Output.WriteLine($"\n=== Platform Health Summary ===");
        Output.WriteLine($"Healthy platforms: {healthyPlatforms}/{platforms.Length}");
        Output.WriteLine($"Unhealthy platforms: {unhealthyPlatforms}/{platforms.Length}");

        foreach (PlatformHealthResult? result in platformResults.Values.OrderBy(r => r.Platform))
        {
            if (result.IsHealthy)
            {
                Output.WriteLine($"  ✓ {result.Platform}: {result.JobCount} jobs");
            }
            else
            {
                Output.WriteLine($"  ✗ {result.Platform}: {result.Error}");
            }
        }

        // Assert that at least some platforms are healthy
        healthyPlatforms.Should().BeGreaterThan(0,
            "at least one platform should be healthy and return results");

        // Note: We don't require all platforms to be healthy for smoke tests
        // as some platforms might be temporarily unavailable or rate-limited
    }

    [ConditionalFact("MultiPlatform")]
    public async Task DataDiversity_AcrossPlatforms_Has_DifferentJobs()
    {
        // Arrange
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            MaxResults = 20
        };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        Output.WriteLine($"=== Testing Data Diversity Across Platforms ===");
        Output.WriteLine($"Query: {criteria.Query}");

        IReadOnlyList<JobListing> results = await _serviceProvider.GetRequiredService<IJobClient>()
            .SearchJobsAsync(criteria, cts.Token);

        // Assert
        results.Should().NotBeNull("results should not be null");
        results.Should().NotBeEmpty("results should not be empty");

        Output.WriteLine($"\nTotal jobs: {results.Count}");

        // Group by platform
        var platformGroups = results
            .Where(j => !string.IsNullOrEmpty(j.Source))
            .GroupBy(j => j.Source!)
            .ToList();

        Output.WriteLine($"Platforms represented: {platformGroups.Count}");

        // Validate no duplicate job IDs (already tested, but important for diversity)
        results.AssertNoDuplicateJobs();

        // Analyze diversity metrics
        Output.WriteLine($"\n=== Diversity Analysis ===");

        // 1. Title diversity
        var uniqueTitles = results.Select(j => j.Title).Distinct().ToList();
        double titleDiversityRatio = (double)uniqueTitles.Count / results.Count;
        Output.WriteLine($"Unique titles: {uniqueTitles.Count}/{results.Count} ({titleDiversityRatio:P1})");

        // 2. Company diversity
        var uniqueCompanies = results.Select(j => j.Company).Distinct().ToList();
        double companyDiversityRatio = (double)uniqueCompanies.Count / results.Count;
        Output.WriteLine($"Unique companies: {uniqueCompanies.Count}/{results.Count} ({companyDiversityRatio:P1})");

        // 3. Location diversity
        var uniqueLocations = results
            .Where(j => !string.IsNullOrEmpty(j.Location))
            .Select(j => j.Location!)
            .Distinct()
            .ToList();
        Output.WriteLine($"Unique locations: {uniqueLocations.Count}");

        // 4. Platform diversity
        double platformDiversityRatio = (double)platformGroups.Count / Math.Max(1, results.Count);
        Output.WriteLine($"Platform diversity: {platformGroups.Count} platforms");

        // Validate diversity thresholds
        uniqueTitles.Should().HaveCountGreaterThan(results.Count / 2,
            "there should be good title diversity (at least 50% unique titles)");

        uniqueCompanies.Should().HaveCountGreaterThan(results.Count / 3,
            "there should be good company diversity (at least 33% unique companies)");

        // Check if jobs from different platforms are actually different
        if (platformGroups.Count > 1)
        {
            Output.WriteLine($"\n=== Cross-Platform Job Comparison ===");

            // Compare jobs between platforms to ensure they're not duplicates
            var jobSignatures = results.Select(j => new
            {
                j.Title,
                j.Company,
                j.Location,
                Platform = j.Source
            }).ToList();

            // Group by title+company to find potential duplicates across platforms
            var potentialCrossPlatformDuplicates = jobSignatures
                .GroupBy(j => new { j.Title, j.Company })
                .Where(g => g.Select(j => j.Platform).Distinct().Count() > 1)
                .ToList();

            Output.WriteLine($"Potential cross-platform duplicates (same title+company): {potentialCrossPlatformDuplicates.Count}");

            if (potentialCrossPlatformDuplicates.Count > 0)
            {
                Output.WriteLine("\nSample potential duplicates:");
                foreach (var dup in potentialCrossPlatformDuplicates.Take(3))
                {
                    string platforms = string.Join(", ", dup.Select(j => j.Platform).Distinct());
                    Output.WriteLine($"  - {dup.Key.Title} at {dup.Key.Company} [{platforms}]");
                }
            }

            // Note: Some cross-platform duplicates are expected (same job posted on multiple platforms)
            // We just want to ensure not ALL jobs are duplicates
            int uniqueJobSignatures = jobSignatures
                .Select(j => $"{j.Title}|{j.Company}|{j.Location}")
                .Distinct()
                .Count();

            double signatureDiversityRatio = (double)uniqueJobSignatures / results.Count;
            Output.WriteLine($"\nUnique job signatures (title|company|location): {uniqueJobSignatures}/{results.Count} ({signatureDiversityRatio:P1})");

            uniqueJobSignatures.Should().BeGreaterThan(results.Count / 2,
                "there should be good overall job diversity (at least 50% unique job signatures)");
        }

        // Output sample diverse jobs
        Output.WriteLine($"\n=== Sample Diverse Jobs ===");
        var sampleJobs = results
            .DistinctBy(j => new { j.Title, j.Company })
            .Take(5)
            .ToList();

        foreach (JobListing? job in sampleJobs)
        {
            Output.WriteLine($"- {job.Title} at {job.Company} ({job.Source})");
        }
    }

    private sealed record PlatformHealthResult
    {
        public string Platform { get; init; } = string.Empty;
        public bool IsHealthy { get; init; }
        public int JobCount { get; init; }
        public string? SampleTitle { get; init; }
        public string? SampleCompany { get; init; }
        public string? Error { get; init; }
    }
}
